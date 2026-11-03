using System.Text.RegularExpressions;
using CrewPilot.Agents.Abstractions;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Core.WorkItems;
using CrewPilot.Core.WorkLogs;
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

    public AgentRunExecutor(
        CrewPilotDbContext db,
        IAgentEngine engine,
        ISecretProtector secrets)
    {
        _db = db;
        _engine = engine;
        _secrets = secrets;
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
        return run;
    }

    /// <summary>执行一次 AgentRun（由后台 Worker 调用）。</summary>
    public async Task ExecuteAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.AgentRuns
            .Include(r => r.Agent)
            .ThenInclude(a => a.Provider)
            .Include(r => r.WorkItem)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || run.Status is AgentRunStatus.Running or AgentRunStatus.Succeeded)
        {
            return;
        }

        run.Status = AgentRunStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 项目目录隔离 + git worktree：开发 Agent 按工作项独立作业
        var workingDirectory = await PrepareWorkingDirectoryAsync(run, ct);
        run.WorkingDirectory = workingDirectory;
        await _db.SaveChangesAsync(ct);

        var apiKey = string.Empty;
        try
        {
            apiKey = _secrets.Unprotect(run.Agent.Provider.ApiKeyProtected);
        }
        catch (Exception ex)
        {
            await FinishAsync(run, false, string.Empty, $"API key 解密失败：{ex.Message}", null, null, null, ct);
            return;
        }

        var request = new AgentRunRequest(
            AgentName: run.Agent.Name,
            Prompt: run.Prompt,
            SystemPrompt: run.Agent.SystemPrompt,
            Model: run.Agent.Model,
            BaseUrl: run.Agent.Provider.BaseUrl,
            ApiKey: apiKey,
            WorkingDirectory: workingDirectory,
            Tools: ParseTools(run.Agent.Tools),
            CommandWhitelist: ParseTools(run.Agent.CommandWhitelist));

        var result = await _engine.RunAsync(request, ct);

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
    }

    /// <summary>取消（被接管）：仅对未结束的运行有效。</summary>
    public async Task<bool> CancelAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || run.Status is AgentRunStatus.Succeeded or AgentRunStatus.Failed or AgentRunStatus.Canceled)
        {
            return false;
        }

        run.Status = AgentRunStatus.Canceled;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.Error = "已被用户取消（接管）";
        if (run.WorkItemId is Guid workItemId)
        {
            _db.Activities.Add(new Activity
            {
                ProjectId = (await _db.WorkItems.Where(w => w.Id == workItemId).Select(w => w.ProjectId).FirstOrDefaultAsync(ct)),
                WorkItemId = workItemId,
                EventType = "agentrun.canceled",
                Payload = "{}",
                ActorKind = MemberKind.User,
            });
        }

        await _db.SaveChangesAsync(ct);
        return true;
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

        // 工时 1:1 折算（不加权）
        if (run.Duration is { } duration && duration > TimeSpan.Zero)
        {
            var hours = AgentRunHours.FromDuration(duration);
            if (run.WorkItemId is Guid workItemId && hours > 0)
            {
                _db.WorkLogs.Add(new WorkLog
                {
                    WorkItemId = workItemId,
                    Hours = hours,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow),
                    Note = $"AgentRun 自动折算（{run.Trigger}）",
                    AgentRunId = run.Id,
                });
            }
        }

        if (run.WorkItemId is Guid id)
        {
            var workItem = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (workItem is not null)
            {
                // PR 链接回填
                if (workItem.PrUrl is null)
                {
                    var match = PrUrlPattern.Match(output ?? string.Empty);
                    if (match.Success)
                    {
                        workItem.PrUrl = match.Value;
                    }
                }

                _db.Activities.Add(new Activity
                {
                    ProjectId = workItem.ProjectId,
                    WorkItemId = workItem.Id,
                    EventType = succeeded ? "agentrun.succeeded" : "agentrun.failed",
                    Payload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        duration = run.Duration?.ToString(),
                        promptTokens,
                        completionTokens,
                    }),
                    ActorKind = MemberKind.Agent,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
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
            var (exitCode, _) = await ProcessRunner.RunAsync(
                localPath,
                $"git worktree add \"{worktreePath}\" -b {branch}",
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
        }
        catch (Exception ex)
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
