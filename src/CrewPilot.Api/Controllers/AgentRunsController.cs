using CrewPilot.Agents.Execution;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class AgentRunsController(
    CrewPilotDbContext db,
    AgentRunQueue queue,
    CrewPilot.Agents.Execution.AgentRunStreamHub streamHub) : ControllerBase
{
    private static readonly System.Text.Json.JsonSerializerOptions SseJson = new(
        System.Text.Json.JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [HttpGet("api/agent-runs")]
    public async Task<ActionResult<IReadOnlyList<AgentRunListItemDto>>> List(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? workItemId,
        [FromQuery] Guid? agentId,
        CancellationToken ct = default)
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

    /// <summary>重试失败/取消的 AgentRun：复制提示词与关联信息，新建一次排队执行。</summary>
    [HttpPost("api/agent-runs/{id:guid}/retry")]
    public async Task<ActionResult<AgentRunListItemDto>> Retry(Guid id, CancellationToken ct)
    {
        var run = await db.AgentRuns
            .Include(r => r.Agent)
            .Include(r => r.WorkItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
        {
            return Problem(detail: "AgentRun 不存在", statusCode: 404);
        }

        if (run.Status is not (AgentRunStatus.Failed or AgentRunStatus.Canceled))
        {
            return Problem(detail: "仅失败或已取消的执行可以重试", statusCode: 400);
        }

        if (run.WorkflowRunId is not null)
        {
            return Problem(detail: "工作流执行请通过卡点/工作流重试处理", statusCode: 400);
        }

        var retry = new AgentRun
        {
            AgentId = run.AgentId,
            WorkItemId = run.WorkItemId,
            Trigger = "retry",
            Prompt = run.Prompt,
            Status = AgentRunStatus.Queued,
        };
        db.AgentRuns.Add(retry);

        if (run.WorkItem is not null)
        {
            db.Activities.Add(new Activity
            {
                ProjectId = run.WorkItem.ProjectId,
                WorkItemId = run.WorkItem.Id,
                EventType = "agentrun.retried",
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    originalRunId = run.Id,
                    retryRunId = retry.Id,
                    agent = run.Agent.Name,
                }),
                ActorKind = MemberKind.User,
            });
        }

        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(retry.Id);

        return Created($"/api/agent-runs/{retry.Id}",
            new AgentRunListItemDto(
                retry.Id, run.Agent.Name, retry.WorkItemId,
                run.WorkItem?.Title, retry.Trigger, "queued", null,
                retry.CreatedAt, null, null, null, null));
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

    /// <summary>流式运行事件（SSE）：status → log... → done，供前端实时展示。</summary>
    [HttpGet("api/agent-runs/{id:guid}/stream")]
    public async Task Stream(Guid id, CancellationToken ct)
    {
        var exists = await db.AgentRuns.AnyAsync(r => r.Id == id, ct);
        if (!exists)
        {
            Response.StatusCode = 404;
            return;
        }

        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        async Task WriteEventAsync(string name, object data)
        {
            await Response.WriteAsync($"event: {name}\ndata: ", ct);
            await Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(data, SseJson), ct);
            await Response.WriteAsync("\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        var lastSequence = -1;
        var lastStatus = string.Empty;
        var (subscriptionId, tokenReader) = streamHub.Subscribe(id);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                while (tokenReader.TryRead(out var delta))
                {
                    await WriteEventAsync("token", new { delta });
                }

                var run = await db.AgentRuns
                .AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new
                {
                    r.Status,
                    r.PromptTokens,
                    r.CompletionTokens,
                    r.Error,
                    r.FinishedAt,
                })
                .FirstAsync(ct);
            var status = run.Status.ToString().ToLowerInvariant();
            if (!string.Equals(status, lastStatus, StringComparison.Ordinal))
            {
                lastStatus = status;
                await WriteEventAsync("status", new
                {
                    status,
                    promptTokens = run.PromptTokens,
                    completionTokens = run.CompletionTokens,
                    error = run.Error,
                    finishedAt = run.FinishedAt,
                });
            }

            var logs = await db.RunLogs
                .AsNoTracking()
                .Where(l => l.AgentRunId == id && l.Sequence > lastSequence)
                .OrderBy(l => l.Sequence)
                .Select(l => new { l.Sequence, l.Kind, l.Content, l.CreatedAt })
                .ToListAsync(ct);
            foreach (var log in logs)
            {
                lastSequence = log.Sequence;
                await WriteEventAsync("log", log);
            }

            if (status is "succeeded" or "failed" or "canceled")
            {
                await WriteEventAsync("done", new { status });
                break;
            }

            await Task.Delay(1000, ct);
            }
        }
        finally
        {
            streamHub.Unsubscribe(id, subscriptionId);
        }
    }
}
