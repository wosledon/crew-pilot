using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class AgentRunsController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet("api/agent-runs")]
    public async Task<ActionResult<IReadOnlyList<AgentRunListItemDto>>> List(        [FromQuery] Guid? projectId, [FromQuery] Guid? workItemId, [FromQuery] Guid? agentId, CancellationToken ct = default)
    {
        var query = db.AgentRuns.AsNoTracking().AsQueryable();
        if (projectId.HasValue)
        {
            query = query.Where(r => r.WorkItem != null && r.WorkItem.ProjectId == projectId);
        }

        if (workItemId.HasValue)
        {
            query = query.Where(r => r.WorkItemId == workItemId);
        }

        if (agentId.HasValue)
        {
            query = query.Where(r => r.AgentId == agentId);
        }

        var rows = await query
            .Select(r => new
            {
                r.Id,
                AgentName = r.Agent.Name,
                r.WorkItemId,
                WorkItemTitle = r.WorkItem != null ? r.WorkItem.Title : null,
                r.Trigger,
                r.Status,
                r.Error,
                r.CreatedAt,
                r.FinishedAt,
                r.Duration,
                r.PromptTokens,
                r.CompletionTokens,
            })
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new AgentRunListItemDto(
                r.Id, r.AgentName, r.WorkItemId, r.WorkItemTitle, r.Trigger,
                r.Status.ToString().ToLowerInvariant(), r.Error,
                r.CreatedAt, r.FinishedAt, r.Duration, r.PromptTokens, r.CompletionTokens))
            .ToList();
        return Ok(items);
    }

    /// <summary>项目内每个工作项最近一次 Agent 活动（优先进行中），供看板展示实时状态。</summary>
    [HttpGet("api/projects/{projectId:guid}/agent-activity")]
    public async Task<ActionResult<IReadOnlyList<WorkItemAgentActivityDto>>> Activity(
        Guid projectId, CancellationToken ct)
    {
        var rows = await db.AgentRuns
            .AsNoTracking()
            .Where(r => r.WorkItem != null && r.WorkItem.ProjectId == projectId &&
                        r.WorkItemId != null)
            .Select(r => new
            {
                r.Id,
                r.WorkItemId,
                AgentName = r.Agent.Name,
                r.Status,
                r.CreatedAt,
                r.FinishedAt,
            })
            .ToListAsync(ct);

        var activities = rows
            .GroupBy(r => r.WorkItemId!.Value)
            .Select(group => group
                .OrderByDescending(r => IsActive(r.Status))
                .ThenByDescending(r => r.CreatedAt)
                .First())
            .Select(r => new WorkItemAgentActivityDto(
                r.WorkItemId!.Value,
                r.Id,
                r.AgentName,
                r.Status.ToString().ToLowerInvariant(),
                (r.FinishedAt ?? r.CreatedAt)))
            .ToList();
        return Ok(activities);
    }

    /// <summary>进行中/等待人工的运行优先于历史运行。</summary>
    private static bool IsActive(AgentRunStatus status) =>
        status is AgentRunStatus.Queued or AgentRunStatus.Running or AgentRunStatus.WaitingForHuman;

    [HttpGet("api/agent-runs/{id:guid}")]
    public async Task<ActionResult<AgentRunDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var run = await db.AgentRuns
            .AsNoTracking()
            .Include(r => r.Agent)
            .Include(r => r.WorkItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
        {
            return Problem(detail: "AgentRun 不存在", statusCode: 404);
        }

        var logs = await db.RunLogs
            .AsNoTracking()
            .Where(l => l.AgentRunId == id)
            .OrderBy(l => l.Sequence)
            .Select(l => new RunLogDto(l.Sequence, l.Kind, l.Content, l.CreatedAt))
            .ToListAsync(ct);

        return new AgentRunDetailDto(
            run.Id, run.Agent.Name, run.Agent.Model, run.WorkItemId,
            run.WorkItem != null ? run.WorkItem.Title : null, run.Trigger,
            run.Status.ToString().ToLowerInvariant(), run.WorkingDirectory,
            run.Prompt, run.Output, run.Error, run.PromptTokens, run.CompletionTokens,
            run.CreatedAt, run.StartedAt, run.FinishedAt, run.Duration, logs);
    }
}
