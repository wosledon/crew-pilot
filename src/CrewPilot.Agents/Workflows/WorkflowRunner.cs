using System.Text;
using CrewPilot.Agents.Execution;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Core.Services;
using CrewPilot.Core.WorkItems;
using CrewPilot.Core.Workflows;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrewPilot.Agents.Workflows;

/// <summary>
/// 工作流运行器：把工作流步骤串成 AgentRun 链，处理审批策略、自动重试与卡点挂起/续跑。
/// 卡点判定：
/// - Agent 输出包含 <see cref="BlockerMarker"/> → Agent 主动上报卡点；
/// - 步骤审批策略为 <see cref="ApprovalPolicy.Always"/> → 完成后挂起等待确认；
/// - 步骤失败且重试耗尽 → 失败卡点。
/// </summary>
public sealed class WorkflowRunner(
    CrewPilotDbContext db,
    AgentRunQueue queue,
    ILogger<WorkflowRunner> logger,
    IRealtimeNotifier? realtime = null)
{
    public const string BlockerMarker = "[[BLOCKER]]";

    private const int MaxSnapshotLength = 16_000;

    /// <summary>启动一次工作流实例：创建 WorkflowRun 并入队第一步。</summary>
    public async Task<WorkflowRun> StartAsync(
        Guid workflowId, Guid workItemId, CancellationToken ct = default)
    {
        var workflow = await db.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.Enabled, ct)
            ?? throw new InvalidOperationException("工作流不存在或已停用");

        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct)
            ?? throw new InvalidOperationException("工作项不存在");

        if (workflow.ProjectId is Guid projectId && projectId != workItem.ProjectId)
        {
            throw new InvalidOperationException("工作流与工作项不属于同一项目");
        }

        var steps = workflow.Steps.OrderBy(s => s.Order).ToList();
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("工作流没有步骤");
        }

        var run = new WorkflowRun
        {
            WorkflowId = workflow.Id,
            WorkItemId = workItem.Id,
            Status = WorkflowRunStatus.Running,
            CurrentStepOrder = steps[0].Order,
            CurrentStepId = steps[0].Id,
        };
        db.WorkflowRuns.Add(run);
        AddActivity(workItem.ProjectId, workItem.Id, "workflow.started", new { workflow = workflow.Name });
        await db.SaveChangesAsync(ct);

        await StartStepAsync(run, steps[0], previousOutput: null, humanInstruction: null, ct);
        logger.LogInformation(
            "工作流启动：workflow={WorkflowId} workItem={WorkItemId} run={RunId}",
            workflow.Id, workItem.Id, run.Id);
        return run;
    }

    /// <summary>AgentRun 结束后由执行器回调：决定重试、挂起卡点或推进下一步。</summary>
    public async Task OnAgentRunFinishedAsync(Guid agentRunId, CancellationToken ct = default)
    {
        var agentRun = await db.AgentRuns
            .Include(r => r.WorkflowRun)!
            .ThenInclude(run => run!.Workflow)
            .ThenInclude(workflow => workflow.Steps)
            .Include(r => r.WorkflowStep)
            .FirstOrDefaultAsync(r => r.Id == agentRunId, ct);
        if (agentRun?.WorkflowRun is null || agentRun.WorkflowStep is null)
        {
            return;
        }

        var run = agentRun.WorkflowRun;
        var step = agentRun.WorkflowStep;
        if (run.Status != WorkflowRunStatus.Running)
        {
            return;
        }

        if (agentRun.Status == AgentRunStatus.Canceled)
        {
            await CancelWorkflowAsync(run, "Agent 运行被用户取消（接管）", ct);
            return;
        }

        if (agentRun.Status != AgentRunStatus.Succeeded)
        {
            var attempts = await db.AgentRuns.CountAsync(
                r => r.WorkflowRunId == run.Id && r.WorkflowStepId == step.Id, ct);
            if (attempts <= step.MaxRetries)
            {
                AddActivity(
                    await GetProjectIdAsync(run, ct),
                    run.WorkItemId,
                    "workflow.step.retry",
                    new { step = step.Name, attempt = attempts });
                await StartStepAsync(
                    run, step, null,
                    $"上一次执行失败：{agentRun.Error ?? "未知错误"}，请修正后重新执行。", ct);
                return;
            }

            await CreateBlockerAsync(
                run, agentRun, step, BlockerKind.Failure,
                $"步骤「{step.Name}」执行失败且重试耗尽：{agentRun.Error ?? "未知错误"}",
                BuildSnapshot(run, step, agentRun), ct);
            return;
        }

        var output = agentRun.Output ?? string.Empty;

        if (TryExtractBlocker(output, out var reason))
        {
            agentRun.Status = AgentRunStatus.WaitingForHuman;
            await CreateBlockerAsync(
                run, agentRun, step, BlockerKind.AgentBlocked, reason,
                BuildSnapshot(run, step, agentRun), ct);
            return;
        }

        if (step.ApprovalPolicy == ApprovalPolicy.Always)
        {
            agentRun.Status = AgentRunStatus.WaitingForHuman;
            await CreateBlockerAsync(
                run, agentRun, step, BlockerKind.Approval,
                $"步骤「{step.Name}」已完成，等待确认后继续",
                BuildSnapshot(run, step, agentRun), ct);
            return;
        }

        await AdvanceAsync(run, step, output, ct);
    }

    /// <summary>处理卡点：继续 / 修改方向 / 取消，并触发续跑。</summary>
    public async Task<bool> ResolveBlockerAsync(
        Guid blockerId, BlockerResolution resolution, string? note, CancellationToken ct = default)
    {
        var blocker = await db.Blockers
            .Include(b => b.WorkflowRun)!
            .ThenInclude(run => run!.Workflow)
            .ThenInclude(workflow => workflow.Steps)
            .Include(b => b.AgentRun)
            .FirstOrDefaultAsync(b => b.Id == blockerId, ct);
        if (blocker is null || blocker.Status != BlockerStatus.Open)
        {
            return false;
        }

        blocker.Status = BlockerStatus.Resolved;
        blocker.Resolution = resolution;
        blocker.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        blocker.ResolvedAt = DateTimeOffset.UtcNow;

        var run = blocker.WorkflowRun;
        if (run is null)
        {
            await db.SaveChangesAsync(ct);
            await NotifyBlockerAsync(blocker.ProjectId, blocker.Id, "resolved", ct);
            return true;
        }

        if (blocker.AgentRun is { Status: AgentRunStatus.WaitingForHuman } pendingRun)
        {
            pendingRun.Status = resolution == BlockerResolution.Redirect
                ? AgentRunStatus.Canceled
                : AgentRunStatus.Succeeded;
        }

        var projectId = await GetProjectIdAsync(run, ct);
        AddActivity(projectId, run.WorkItemId, "blocker.resolved",
            new { blockerId = blocker.Id, resolution = resolution.ToString(), note });

        if (resolution == BlockerResolution.Cancel)
        {
            run.Status = WorkflowRunStatus.Canceled;
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.UpdatedAt = DateTimeOffset.UtcNow;
            AddActivity(projectId, run.WorkItemId, "workflow.canceled", new { run = run.Id });
            await db.SaveChangesAsync(ct);
            await NotifyBlockerAsync(projectId, blocker.Id, "resolved", ct);
            return true;
        }

        var step = await ResolveBlockerStepAsync(blocker, run, ct);
        if (step is null)
        {
            run.Status = WorkflowRunStatus.Failed;
            run.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await NotifyBlockerAsync(projectId, blocker.Id, "resolved", ct);
            return true;
        }

        run.Status = WorkflowRunStatus.Running;
        run.UpdatedAt = DateTimeOffset.UtcNow;

        if (blocker.Kind == BlockerKind.Approval && resolution == BlockerResolution.Continue)
        {
            await AdvanceAsync(run, step, blocker.AgentRun?.Output, ct);
        }
        else
        {
            await StartStepAsync(run, step, blocker.AgentRun?.Output, note, ct);
        }

        await NotifyBlockerAsync(projectId, blocker.Id, "resolved", ct);
        return true;
    }

    /// <summary>取消工作流实例（关闭未处理卡点）。</summary>
    public async Task<bool> CancelRunAsync(Guid workflowRunId, CancellationToken ct = default)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);
        if (run is null || run.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Canceled)
        {
            return false;
        }

        run.Status = WorkflowRunStatus.Canceled;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.UpdatedAt = DateTimeOffset.UtcNow;

        var openBlockers = await db.Blockers
            .Where(b => b.WorkflowRunId == run.Id && b.Status == BlockerStatus.Open)
            .ToListAsync(ct);
        foreach (var blocker in openBlockers)
        {
            blocker.Status = BlockerStatus.Resolved;
            blocker.Resolution = BlockerResolution.Cancel;
            blocker.ResolvedAt = DateTimeOffset.UtcNow;
        }

        AddActivity(await GetProjectIdAsync(run, ct), run.WorkItemId,
            "workflow.canceled", new { run = run.Id });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>判断工作流运行中某一步是否有可用于卡点上下文的 AgentRun。</summary>
    public static bool TryExtractBlocker(string output, out string reason)
    {
        var index = output.IndexOf(BlockerMarker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            reason = string.Empty;
            return false;
        }

        var after = output[(index + BlockerMarker.Length)..].Trim();
        reason = after.Length == 0
            ? "Agent 上报了卡点，但未说明原因"
            : after.Length > 2000 ? after[..2000] : after;
        return true;
    }

    private async Task StartStepAsync(
        WorkflowRun run,
        WorkflowStep step,
        string? previousOutput,
        string? humanInstruction,
        CancellationToken ct)
    {
        var projectId = await GetProjectIdAsync(run, ct);
        var agent = await ResolveAgentAsync(step, projectId, ct);
        if (agent is null)
        {
            await CreateBlockerAsync(
                run, null, step, BlockerKind.Failure,
                $"未找到可执行步骤「{step.Name}」的 Agent（岗位 {step.RequiredJob?.ToString() ?? "未指定"}）",
                null, ct);
            return;
        }

        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == run.WorkItemId, ct);
        if (workItem is null)
        {
            await CreateBlockerAsync(
                run, null, step, BlockerKind.Failure,
                "工作项不存在，无法继续工作流", null, ct);
            return;
        }

        var prompt = await BuildStepPromptAsync(
            run, step, agent, workItem, previousOutput, humanInstruction, ct);

        var agentRun = new AgentRun
        {
            AgentId = agent.Id,
            WorkItemId = run.WorkItemId,
            WorkflowRunId = run.Id,
            WorkflowStepId = step.Id,
            Trigger = "workflow",
            Prompt = prompt,
            Status = AgentRunStatus.Queued,
        };
        db.AgentRuns.Add(agentRun);

        run.Status = WorkflowRunStatus.Running;
        run.CurrentStepId = step.Id;
        run.CurrentStepOrder = step.Order;
        run.UpdatedAt = DateTimeOffset.UtcNow;

        AddActivity(projectId, run.WorkItemId, "workflow.step.queued",
            new { step = step.Name, agent = agent.Name, run = run.Id });
        await db.SaveChangesAsync(ct);

        await queue.EnqueueAsync(agentRun.Id);
        logger.LogInformation(
            "工作流步骤入队：run={RunId} step={Step} agent={Agent} agentRun={AgentRunId}",
            run.Id, step.Name, agent.Name, agentRun.Id);
    }

    private async Task AdvanceAsync(
        WorkflowRun run, WorkflowStep step, string? output, CancellationToken ct)
    {
        var next = await db.WorkflowSteps
            .Where(s => s.WorkflowId == run.WorkflowId && s.Order > step.Order)
            .OrderBy(s => s.Order)
            .FirstOrDefaultAsync(ct);
        if (next is not null)
        {
            await StartStepAsync(run, next, output, null, ct);
            return;
        }

        run.Status = WorkflowRunStatus.Succeeded;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.UpdatedAt = DateTimeOffset.UtcNow;
        run.CurrentStepId = null;

        var projectId = await GetProjectIdAsync(run, ct);
        AddActivity(projectId, run.WorkItemId, "workflow.succeeded",
            new { run = run.Id, steps = step.Order + 1 });
        await TryAdvanceWorkItemAsync(run.WorkItemId, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Blocker> CreateBlockerAsync(
        WorkflowRun run,
        AgentRun? agentRun,
        WorkflowStep step,
        BlockerKind kind,
        string reason,
        string? snapshot,
        CancellationToken ct)
    {
        var projectId = await GetProjectIdAsync(run, ct);
        var blocker = new Blocker
        {
            ProjectId = projectId,
            WorkItemId = run.WorkItemId,
            WorkflowRunId = run.Id,
            AgentRunId = agentRun?.Id,
            Kind = kind,
            Reason = reason,
            ContextSnapshot = snapshot,
            Status = BlockerStatus.Open,
        };
        if (snapshot is { Length: > MaxSnapshotLength })
        {
            blocker.ContextSnapshot = snapshot[..MaxSnapshotLength];
        }

        db.Blockers.Add(blocker);
        run.Status = WorkflowRunStatus.WaitingForHuman;
        run.UpdatedAt = DateTimeOffset.UtcNow;

        AddActivity(projectId, run.WorkItemId, "blocker.created",
            new { kind = kind.ToString(), reason, step = step.Name });
        await db.SaveChangesAsync(ct);
        await NotifyBlockerAsync(projectId, blocker.Id, "open", ct);
        logger.LogInformation(
            "卡点挂起：run={RunId} step={Step} kind={Kind} reason={Reason}",
            run.Id, step.Name, kind, reason);
        return blocker;
    }

    private async Task CancelWorkflowAsync(WorkflowRun run, string reason, CancellationToken ct)
    {
        run.Status = WorkflowRunStatus.Canceled;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.UpdatedAt = DateTimeOffset.UtcNow;

        var openBlockers = await db.Blockers
            .Where(b => b.WorkflowRunId == run.Id && b.Status == BlockerStatus.Open)
            .ToListAsync(ct);
        foreach (var blocker in openBlockers)
        {
            blocker.Status = BlockerStatus.Resolved;
            blocker.Resolution = BlockerResolution.Cancel;
            blocker.ResolutionNote = reason;
            blocker.ResolvedAt = DateTimeOffset.UtcNow;
        }

        AddActivity(await GetProjectIdAsync(run, ct), run.WorkItemId,
            "workflow.canceled", new { run = run.Id, reason });
        await db.SaveChangesAsync(ct);
        foreach (var blocker in openBlockers)
        {
            await NotifyBlockerAsync(blocker.ProjectId, blocker.Id, "resolved", ct);
        }
    }

    private async Task NotifyBlockerAsync(
        Guid projectId, Guid blockerId, string status, CancellationToken ct)
    {
        if (realtime is null)
        {
            return;
        }

        try
        {
            await realtime.BlockerChangedAsync(projectId, blockerId, status, ct);
        }
        catch (Exception)
        {
            // 实时推送失败不影响工作流状态
        }
    }

    private async Task<WorkflowStep?> ResolveBlockerStepAsync(
        Blocker blocker, WorkflowRun run, CancellationToken ct)
    {
        if (blocker.AgentRun?.WorkflowStepId is Guid stepId)
        {
            return await db.WorkflowSteps.FirstOrDefaultAsync(s => s.Id == stepId, ct);
        }

        if (run.CurrentStepId is Guid currentId)
        {
            return await db.WorkflowSteps.FirstOrDefaultAsync(s => s.Id == currentId, ct);
        }

        return run.Workflow.Steps.OrderBy(s => s.Order).FirstOrDefault();
    }

    private async Task<Agent?> ResolveAgentAsync(
        WorkflowStep step, Guid projectId, CancellationToken ct)
    {
        if (step.AgentId is Guid agentId)
        {
            var explicitAgent = await db.Agents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.Enabled, ct);
            if (explicitAgent is not null)
            {
                return explicitAgent;
            }
        }

        if (step.RequiredJob is AgentJob job)
        {
            // 优先项目默认团队中同岗位的 Agent
            var defaultTeamId = await db.Projects
                .Where(p => p.Id == projectId)
                .Select(p => p.DefaultTeamId)
                .FirstOrDefaultAsync(ct);
            if (defaultTeamId is Guid teamId)
            {
                var teamAgent = await db.AgentTeams
                    .Where(t => t.Id == teamId)
                    .SelectMany(t => t.Members)
                    .Where(m => m.Agent.Enabled && m.Agent.Job == job)
                    .Select(m => m.Agent)
                    .OrderBy(a => a.Name)
                    .FirstOrDefaultAsync(ct);
                if (teamAgent is not null)
                {
                    return teamAgent;
                }
            }

            return await db.Agents
                .Where(a => a.Enabled && a.Job == job)
                .OrderBy(a => a.Name)
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    private async Task<string> BuildStepPromptAsync(
        WorkflowRun run,
        WorkflowStep step,
        Agent agent,
        WorkItem workItem,
        string? previousOutput,
        string? humanInstruction,
        CancellationToken ct)
    {
        var workflowName = await db.Workflows
            .Where(w => w.Id == run.WorkflowId)
            .Select(w => w.Name)
            .FirstOrDefaultAsync(ct) ?? "工作流";
        var totalSteps = await db.WorkflowSteps.CountAsync(s => s.WorkflowId == run.WorkflowId, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"你是 {agent.Name}（岗位：{agent.Job}）。");
        sb.AppendLine($"你正在执行项目工作流「{workflowName}」第 {step.Order + 1}/{Math.Max(totalSteps, step.Order + 1)} 步：{step.Name}。");
        if (!string.IsNullOrWhiteSpace(step.Instruction))
        {
            sb.AppendLine($"步骤要求：{step.Instruction}");
        }

        if (!string.IsNullOrWhiteSpace(step.InputContract))
        {
            sb.AppendLine($"输入契约：{step.InputContract}");
        }

        if (!string.IsNullOrWhiteSpace(step.OutputContract))
        {
            sb.AppendLine($"输出契约：{step.OutputContract}");
        }

        sb.AppendLine();
        sb.AppendLine($"工作项：{workItem.Title}");
        if (!string.IsNullOrWhiteSpace(workItem.Description))
        {
            sb.AppendLine($"描述：{Truncate(workItem.Description, 4000)}");
        }

        if (!string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
        {
            sb.AppendLine($"验收标准：{Truncate(workItem.AcceptanceCriteria, 4000)}");
        }

        if (!string.IsNullOrWhiteSpace(previousOutput))
        {
            sb.AppendLine();
            sb.AppendLine($"上一步输出：{Truncate(previousOutput, 6000)}");
        }

        if (!string.IsNullOrWhiteSpace(humanInstruction))
        {
            sb.AppendLine();
            sb.AppendLine($"人工补充指示：{Truncate(humanInstruction, 2000)}");
        }

        sb.AppendLine();
        sb.AppendLine("完成后给出结论。");
        sb.AppendLine($"如果遇到无法自行决策的事项，请单独一行写出：{BlockerMarker} 需要人工决策的具体原因。");
        return sb.ToString();
    }

    private async Task TryAdvanceWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct);
        if (workItem is null)
        {
            return;
        }

        try
        {
            switch (workItem)
            {
                case Requirement requirement when requirement.Status == RequirementStatus.InProgress:
                    WorkItemStateMachine.Transition(requirement, RequirementStatus.InReview);
                    break;
                case Defect defect when defect.Status == DefectStatus.Fixing:
                    WorkItemStateMachine.Transition(defect, DefectStatus.PendingVerification);
                    break;
                case TaskItem task:
                    WorkItemStateMachine.Transition(task, done: true);
                    break;
            }

            workItem.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "工作流完成后自动流转工作项失败：{WorkItemId}", workItemId);
        }
    }

    private async Task<Guid> GetProjectIdAsync(WorkflowRun run, CancellationToken ct) =>
        await db.WorkItems
            .Where(w => w.Id == run.WorkItemId)
            .Select(w => w.ProjectId)
            .FirstOrDefaultAsync(ct);

    private string BuildSnapshot(WorkflowRun run, WorkflowStep step, AgentRun? agentRun)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"工作流：{run.Workflow?.Name ?? run.WorkflowId.ToString()}");
        sb.AppendLine($"步骤：{step.Order + 1}. {step.Name}");
        if (agentRun is not null)
        {
            sb.AppendLine($"Agent：{agentRun.AgentId}");
            sb.AppendLine($"状态：{agentRun.Status}");
            if (!string.IsNullOrWhiteSpace(agentRun.Error))
            {
                sb.AppendLine($"错误：{agentRun.Error}");
            }

            if (!string.IsNullOrWhiteSpace(agentRun.Output))
            {
                sb.AppendLine($"输出：{Truncate(agentRun.Output, 8000)}");
            }
        }

        return sb.ToString();
    }

    private void AddActivity(Guid projectId, Guid workItemId, string eventType, object payload)
    {
        db.Activities.Add(new Activity
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            EventType = eventType,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            ActorKind = MemberKind.Agent,
        });
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
