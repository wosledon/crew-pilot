using System.Text.RegularExpressions;
using CrewPilot.Agents.Abstractions;
using CrewPilot.Agents.Workflows;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Audit;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Core.WorkItems;
using CrewPilot.Core.WorkLogs;
using CrewPilot.Infrastructure.Execution;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Agents.Execution;

/// <summary>
/// AgentRun 执行器：认领闭环的核心。
/// 入队 → 准备工作目录（worktree）→ 调引擎 → 落日志/产物/token → 工时 1:1 折算 → 活动流。
/// </summary>
public sealed class AgentRunExecutor
{
    private static readonly Regex PrUrlPattern = new(
        @"https?://\S+/(?:pull|merge_requests)/\d+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly CrewPilotDbContext _db;
    private readonly IAgentEngine _engine;
    private readonly ISecretProtector _secrets;
    private readonly AgentRunCancellationRegistry _cancellation;
    private readonly WorkflowRunner _workflowRunner;
    private readonly IRealtimeNotifier? _realtime;

    public AgentRunExecutor(
        CrewPilotDbContext db,
        IAgentEngine engine,
        ISecretProtector secrets,
        AgentRunCancellationRegistry cancellation,
        WorkflowRunner workflowRunner,
        IRealtimeNotifier? realtime = null)
    {
        _db = db;
        _engine = engine;
        _secrets = secrets;
        _cancellation = cancellation;
        _workflowRunner = workflowRunner;
        _realtime = realtime;
    }

    /// <summary>创建工作项被 Agent 认领的执行记录并入队。</summary>
    public async Task<AgentRun> EnqueueForWorkItemAsync(
        Guid agentId, Guid workItemId, string trigger, CancellationToken ct = default)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId, ct)
            ?? throw new InvalidOperationException("Agent 不存在");
        var workItem = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct)
            ?? throw new InvalidOperationException("工作项不存在");

        var run = new AgentRun
        {
            AgentId = agent.Id,
            WorkItemId = workItem.Id,
            Trigger = trigger,
            Prompt = BuildPrompt(agent, workItem),
            Status = AgentRunStatus.Queued,
        };
        _db.AgentRuns.Add(run);

        _db.Activities.Add(new Activity
        {
            ProjectId = workItem.ProjectId,
            WorkItemId = workItem.Id,
            EventType = "agentrun.queued",
            Payload = System.Text.Json.JsonSerializer.Serialize(new { agent = agent.Name, trigger }),
            ActorKind = MemberKind.Agent,
        });

        await _db.SaveChangesAsync(ct);
        await NotifyRunAsync(run, workItem.ProjectId, ct);
        return run;
    }

    /// <summary>执行一次 AgentRun（由后台 Worker 调用）。仅处理排队中的运行。</summary>
    public async Task ExecuteAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.AgentRuns
            .Include(r => r.Agent)
            .ThenInclude(a => a.Provider)
            .Include(r => r.Agent)
            .ThenInclude(a => a.Skills)
            .ThenInclude(s => s.Skill)
            .Include(r => r.Agent)
            .ThenInclude(a => a.McpServers)
            .ThenInclude(m => m.McpServer)
            .Include(r => r.WorkItem)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || run.Status != AgentRunStatus.Queued)
        {
            // 已取消/已结束的运行不重复执行；工作流运行取消时通知运行器收尾
            if (run is { Status: AgentRunStatus.Canceled, WorkflowRunId: not null })
            {
                await NotifyWorkflowAsync(run, ct);
            }

            return;
        }

        run.Status = AgentRunStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await NotifyRunAsync(run, run.WorkItem?.ProjectId ?? Guid.Empty, ct);

        using var runCts = _cancellation.Register(run.Id, ct);
        McpToolSession? mcpSession = null;
        try
        {
            // MCP 工具会话：绑定 Server 可用才注入，单个 Server 失败自动跳过
            mcpSession = await McpToolSession.ConnectAsync(
                run.Agent.McpServers, _secrets, ct);

            // 取消可能发生在状态落库与注册之间，注册后再确认一次
            if (await IsCanceledInDbAsync(run.Id, ct))
            {
                await FinishCanceledAsync(run, ct);
                return;
            }

            // 项目目录隔离 + git worktree：开发 Agent 按工作项独立作业
            var workingDirectory = await PrepareWorkingDirectoryAsync(run, runCts.Token);
            run.WorkingDirectory = workingDirectory;
            await _db.SaveChangesAsync(ct);

            var apiKey = string.Empty;
            try
            {
                apiKey = _secrets.Unprotect(run.Agent.Provider.ApiKeyProtected);
            }
            catch (Exception ex)
            {
                await FinishAsync(
                    run,
                    false,
                    string.Empty,
                    $"API key 解密失败：{ex.Message}",
                    null,
                    null,
                    null,
                    ct);
                return;
            }

            var request = new AgentRunRequest(
                AgentName: run.Agent.Name,
                Prompt: run.Prompt,
                SystemPrompt: ComposeSystemPrompt(run.Agent, run.WorkItem),
                Model: run.Agent.Model,
                BaseUrl: run.Agent.Provider.BaseUrl,
                ApiKey: apiKey,
                WorkingDirectory: workingDirectory,
                Tools: ParseTools(run.Agent.Tools),
                CommandWhitelist: ParseTools(run.Agent.CommandWhitelist),
                McpTools: mcpSession.Tools,
                McpInvoker: mcpSession.Invoker,
                SandboxPolicy: run.Agent.SandboxPolicy);

            var result = await _engine.RunAsync(request, runCts.Token);

            // 引擎可能忽略取消令牌直接返回，这里再确认一次取消状态
            if (runCts.IsCancellationRequested || await IsCanceledInDbAsync(run.Id, ct))
            {
                await FinishCanceledAsync(run, ct);
                await NotifyWorkflowAsync(run, ct);
                return;
            }

            var sequence = 0;
            if (result.Steps is not null)
            {
                foreach (var step in result.Steps)
                {
                    _db.RunLogs.Add(new RunLog
                    {
                        AgentRunId = run.Id,
                        Sequence = ++sequence,
                        Kind = step.Kind,
                        Content = step.Content,
                    });
                }
            }

            await FinishAsync(
                run,
                result.Succeeded,
                result.Output,
                result.Error,
                result.PromptTokens,
                result.CompletionTokens,
                result.Duration,
                ct);
            await NotifyWorkflowAsync(run, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 用户取消（接管）：保留状态、日志与已耗工时
            await FinishCanceledAsync(run, ct);
            await NotifyWorkflowAsync(run, ct);
        }
        finally
        {
            _cancellation.Unregister(run.Id, runCts);
            await CleanupWorktreeAsync(run);
            if (mcpSession is not null)
            {
                var calls = mcpSession.Calls;
                await mcpSession.DisposeAsync();
                await FlushMcpAuditAsync(run, calls, CancellationToken.None);
            }
        }
    }

    /// <summary>MCP 工具级审计：每次 tools/call 记录服务器、工具、耗时与结果（不含参数原文）。</summary>
    private async Task FlushMcpAuditAsync(
        AgentRun run,
        IReadOnlyList<McpToolCallAudit> calls,
        CancellationToken ct)
    {
        if (calls.Count == 0)
        {
            return;
        }

        foreach (var call in calls)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ProjectId = run.WorkItem?.ProjectId,
                Action = "mcp.tool.called",
                TargetType = "mcp-tool",
                TargetId = $"{call.ServerName}:{call.ToolName}",
                Actor = "agent",
                Detail = System.Text.Json.JsonSerializer.Serialize(new
                {
                    runId = run.Id,
                    workItemId = run.WorkItemId,
                    agent = run.Agent.Name,
                    call.Succeeded,
                    call.ElapsedMilliseconds,
                    error = call.Error,
                    call.CalledAt,
                }),
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // 审计落库失败不阻断运行收尾（状态/日志仍会保存）
        }
    }

    /// <summary>取消（被接管）：运行中则中止引擎调用，排队中则直接置为已取消。</summary>
    public async Task<bool> CancelAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || run.Status is AgentRunStatus.Succeeded or AgentRunStatus.Failed or AgentRunStatus.Canceled)
        {
            return false;
        }

        // 正在执行：向引擎发取消信号，由执行器负责收尾（落状态/日志/工时）
        if (_cancellation.Cancel(runId))
        {
            return true;
        }

        // 排队中（尚未开始）：直接落已取消，Worker 出队时会被守卫跳过
        run.Status = AgentRunStatus.Canceled;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.Error = "已被用户取消（接管）";

        if (run.WorkItemId is Guid workItemId)
        {
            _db.Activities.Add(new Activity
            {
                ProjectId = await _db.WorkItems
                    .Where(w => w.Id == workItemId)
                    .Select(w => w.ProjectId)
                    .FirstOrDefaultAsync(ct),
                WorkItemId = workItemId,
                EventType = "agentrun.canceled",
                Payload = "{}",
                ActorKind = MemberKind.User,
            });
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>系统提示词 = Agent 基础提示词（模板变量替换）+ 已装配且启用的 Skills 内容。</summary>
    private static string ComposeSystemPrompt(Agent agent, WorkItem? workItem)
    {
        var basePrompt = workItem is null
            ? agent.SystemPrompt
            : PromptTemplate.Render(agent.SystemPrompt, workItem);
        var skills = agent.Skills
            .Select(link => link.Skill)
            .Where(skill => skill.Enabled)
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .ToList();
        if (skills.Count == 0)
        {
            return basePrompt;
        }

        var builder = new System.Text.StringBuilder(basePrompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## 已装配技能");
        foreach (var skill in skills)
        {
            builder.AppendLine($"### {skill.Name}");
            builder.AppendLine(workItem is null
                ? skill.PromptContent
                : PromptTemplate.Render(skill.PromptContent, workItem));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>AgentRun 结束后推进工作流（仅工作流触发的运行）。</summary>
    private async Task NotifyWorkflowAsync(AgentRun run, CancellationToken ct)
    {
        if (run.WorkflowRunId is null)
        {
            return;
        }

        try
        {
            await _workflowRunner.OnAgentRunFinishedAsync(run.Id, ct);
        }
        catch (Exception ex)
        {
            _db.RunLogs.Add(new RunLog
            {
                AgentRunId = run.Id,
                Sequence = 9_997,
                Kind = "error",
                Content = $"工作流推进失败：{ex.Message}",
            });
            await _db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task FinishAsync(
        AgentRun run,
        bool succeeded,
        string output,
        string? error,
        int? promptTokens,
        int? completionTokens,
        TimeSpan? engineDuration,
        CancellationToken ct)
    {
        // 并发取消保护：DB 中已被置为取消时不再覆盖为成功/失败
        if (await IsCanceledInDbAsync(run.Id, ct))
        {
            await FinishCanceledAsync(run, ct);
            return;
        }

        run.Status = succeeded ? AgentRunStatus.Succeeded : AgentRunStatus.Failed;
        run.FinishedAt = DateTimeOffset.UtcNow;
        // 优先采用引擎测量的耗时（工时 1:1 折算的依据）
        run.Duration = engineDuration is { } reported && reported > TimeSpan.Zero
            ? reported
            : run.FinishedAt - run.StartedAt;
        run.Output = string.IsNullOrWhiteSpace(output) ? null : output;
        run.Error = error;
        run.PromptTokens = promptTokens;
        run.CompletionTokens = completionTokens;

        await AddAutoWorkLogAsync(run, "AgentRun 自动折算");
        AddPrUrlIfFound(run, output);
        AddRunActivity(run, succeeded ? "agentrun.succeeded" : "agentrun.failed", promptTokens, completionTokens);
        await AddMentionReplyAsync(run);

        await _db.SaveChangesAsync(ct);
        await NotifyRunAsync(run, run.WorkItem?.ProjectId ?? Guid.Empty, ct);
    }

    private async Task FinishCanceledAsync(AgentRun run, CancellationToken ct)
    {
        run.Status = AgentRunStatus.Canceled;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.Duration = run.StartedAt is { } started ? run.FinishedAt - started : null;
        run.Error = "已被用户取消（接管）";

        _db.RunLogs.Add(new RunLog
        {
            AgentRunId = run.Id,
            Sequence = 9_999,
            Kind = "info",
            Content = "运行已被用户取消（接管），保留现场与已耗工时。",
        });

        await AddAutoWorkLogAsync(run, "AgentRun 取消折算（被接管）");
        AddRunActivity(run, "agentrun.canceled", null, null);

        await _db.SaveChangesAsync(ct);
        await NotifyRunAsync(run, run.WorkItem?.ProjectId ?? Guid.Empty, ct);
    }

    private async Task NotifyRunAsync(AgentRun run, Guid projectId, CancellationToken ct)
    {
        if (_realtime is null || projectId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _realtime.AgentRunChangedAsync(
                projectId, run.Id, run.Status.ToString().ToLowerInvariant(), ct);
        }
        catch (Exception)
        {
            // 实时推送失败不影响执行结果（前端仍有轮询兜底）
        }
    }

    private async Task AddAutoWorkLogAsync(AgentRun run, string note)
    {
        if (run.WorkItemId is not Guid workItemId ||
            run.Duration is not { } duration ||
            duration <= TimeSpan.Zero)
        {
            return;
        }

        var hours = AgentRunHours.FromDuration(duration);
        if (hours <= 0)
        {
            return;
        }

        var member = await EnsureAgentMemberAsync(run);
        _db.WorkLogs.Add(new WorkLog
        {
            WorkItemId = workItemId,
            MemberId = member?.Id,
            Hours = hours,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Note = $"{note}（{run.Trigger}）",
            AgentRunId = run.Id,
        });

        // 剩余工时随登记自动扣减（不为负）
        if (run.WorkItem?.RemainingHours is decimal remaining)
        {
            run.WorkItem.RemainingHours = Math.Max(0m, remaining - hours);
        }
    }

    /// <summary>取得（或创建）Agent 对应的项目成员，用于评论/工时归属。</summary>
    private async Task<ProjectMember?> EnsureAgentMemberAsync(AgentRun run)
    {
        var projectId = run.WorkItem?.ProjectId;
        if (projectId is null)
        {
            return null;
        }

        var member = await _db.Members.FirstOrDefaultAsync(
            m => m.ProjectId == projectId && m.DisplayName == run.Agent.Name);
        if (member is not null)
        {
            return member;
        }

        member = new ProjectMember
        {
            ProjectId = projectId.Value,
            Kind = MemberKind.Agent,
            Role = ProjectRole.Developer,
            DisplayName = run.Agent.Name,
        };
        _db.Members.Add(member);
        return member;
    }

    private void AddPrUrlIfFound(AgentRun run, string? output)
    {
        if (run.WorkItem is null || run.WorkItem.PrUrl is not null)
        {
            return;
        }

        var match = PrUrlPattern.Match(output ?? string.Empty);
        if (match.Success)
        {
            run.WorkItem.PrUrl = match.Value;
        }
    }

    private void AddRunActivity(AgentRun run, string eventType, int? promptTokens, int? completionTokens)
    {
        if (run.WorkItem is null)
        {
            return;
        }

        _db.Activities.Add(new Activity
        {
            ProjectId = run.WorkItem.ProjectId,
            WorkItemId = run.WorkItem.Id,
            EventType = eventType,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                duration = run.Duration?.ToString(),
                promptTokens,
                completionTokens,
            }),
            ActorKind = MemberKind.Agent,
        });
    }

    /// <summary>评论 @Agent 触发的运行：把输出（或错误）作为 Agent 回复写入评论线程。</summary>
    private async Task AddMentionReplyAsync(AgentRun run)
    {
        if (run.Trigger != "mention" || run.WorkItem is null)
        {
            return;
        }

        var content = run.Status == AgentRunStatus.Succeeded ? run.Output : run.Error;
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (content.Length > 16_000)
        {
            content = content[..16_000];
        }

        var projectId = run.WorkItem.ProjectId;
        var member = await EnsureAgentMemberAsync(run);
        if (member is null)
        {
            return;
        }
        _db.Comments.Add(new Comment
        {
            WorkItemId = run.WorkItem.Id,
            Author = member,
            AuthorKind = MemberKind.Agent,
            Content = content,
        });
        _db.Activities.Add(new Activity
        {
            ProjectId = projectId,
            WorkItemId = run.WorkItem.Id,
            EventType = "comment.added",
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                author = run.Agent.Name,
                trigger = "mention",
            }),
            ActorKind = MemberKind.Agent,
        });
    }

    private async Task<bool> IsCanceledInDbAsync(Guid runId, CancellationToken ct)
    {
        var status = await _db.AgentRuns
            .AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => r.Status)
            .FirstOrDefaultAsync(ct);
        return status == AgentRunStatus.Canceled;
    }

    private async Task<string?> PrepareWorkingDirectoryAsync(AgentRun run, CancellationToken ct)
    {
        if (run.WorkItemId is not Guid workItemId)
        {
            return null;
        }

        var localPath = await _db.WorkItems
            .Where(w => w.Id == workItemId)
            .Select(w => w.Project.LocalPath)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
        {
            return null;
        }

        // 每个工作项一个独立 worktree：agent/<run 短 id>
        var shortId = run.Id.ToString("N")[..8];
        var worktreesRoot = Path.Combine(localPath, ".crewpilot", "worktrees");
        var worktreePath = Path.Combine(worktreesRoot, shortId);
        var branch = $"agent/{shortId}";

        try
        {
            Directory.CreateDirectory(worktreesRoot);
            var (exitCode, output) = await ProcessRunner.RunAsync(
                localPath,
                "git",
                ["worktree", "add", worktreePath, "-b", branch],
                TimeSpan.FromSeconds(60),
                ct);
            if (exitCode == 0 && Directory.Exists(worktreePath))
            {
                _db.RunLogs.Add(new RunLog
                {
                    AgentRunId = run.Id,
                    Sequence = 0,
                    Kind = "info",
                    Content = $"已创建 git worktree：{worktreePath}（分支 {branch}）",
                });
                await _db.SaveChangesAsync(ct);
                return worktreePath;
            }

            _db.RunLogs.Add(new RunLog
            {
                AgentRunId = run.Id,
                Sequence = 0,
                Kind = "error",
                Content = $"worktree 创建失败（退出码 {exitCode}）：{output}",
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _db.RunLogs.Add(new RunLog
            {
                AgentRunId = run.Id,
                Sequence = 0,
                Kind = "error",
                Content = $"worktree 创建失败：{ex.Message}",
            });
            await _db.SaveChangesAsync(ct);
        }

        // 非 git 仓库或 worktree 失败时退回项目目录（仍受沙箱约束）
        return localPath;
    }

    /// <summary>失败/取消时清理 worktree；成功时保留，等待 PR/合并流程（M4）后清理。</summary>
    private async Task CleanupWorktreeAsync(AgentRun run)
    {
        if (run.Status is not (AgentRunStatus.Canceled or AgentRunStatus.Failed) ||
            string.IsNullOrWhiteSpace(run.WorkingDirectory) ||
            run.WorkItemId is not Guid workItemId)
        {
            return;
        }

        var localPath = await _db.WorkItems
            .AsNoTracking()
            .Where(w => w.Id == workItemId)
            .Select(w => w.Project.LocalPath)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        var worktreesRoot = Path.Combine(localPath, ".crewpilot", "worktrees");
        if (!run.WorkingDirectory.StartsWith(worktreesRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var (exitCode, output) = await ProcessRunner.RunAsync(
                localPath,
                "git",
                ["worktree", "remove", "--force", run.WorkingDirectory],
                TimeSpan.FromSeconds(30));
            if (Directory.Exists(run.WorkingDirectory))
            {
                Directory.Delete(run.WorkingDirectory, recursive: true);
            }

            _db.RunLogs.Add(new RunLog
            {
                AgentRunId = run.Id,
                Sequence = 9_998,
                Kind = exitCode == 0 ? "info" : "error",
                Content = $"worktree 清理：{run.WorkingDirectory}（退出码 {exitCode}）{output}".Trim(),
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _db.RunLogs.Add(new RunLog
            {
                AgentRunId = run.Id,
                Sequence = 9_998,
                Kind = "error",
                Content = $"worktree 清理失败：{ex.Message}",
            });
            await _db.SaveChangesAsync();
        }
    }

    private static string BuildPrompt(Agent agent, WorkItem workItem)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"你是 {agent.Name}（岗位：{agent.Job}）。");
        sb.AppendLine($"工作项：{workItem.Title}");
        if (!string.IsNullOrWhiteSpace(workItem.Description))
        {
            sb.AppendLine($"描述：{workItem.Description}");
        }

        if (!string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
        {
            sb.AppendLine($"验收标准：{workItem.AcceptanceCriteria}");
        }

        sb.AppendLine("请在隔离的工作目录中完成工作，完成后汇报结果；如有 PR 请给出链接。");
        return sb.ToString();
    }

    private static List<string>? ParseTools(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return null;
        }
    }
}
