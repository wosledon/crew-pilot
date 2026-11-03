using CrewPilot.Api.Contracts;
using CrewPilot.Agents.Execution;
using CrewPilot.Core.Agents;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>Agent 认领闭环：指派工作项 / 试运行 / 取消接管。</summary>
[ApiController]
public sealed class AgentClaimsController(
    CrewPilotDbContext db,
    AgentRunExecutor executor,
    AgentRunQueue queue) : ControllerBase
{
    /// <summary>把工作项指派给 Agent：创建 AgentRun 并入队执行。</summary>
    [HttpPost("api/workitems/{id:guid}/assign-agent")]
    public async Task<ActionResult<AgentRunListItemDto>> Assign(
        Guid id, [FromBody] AssignAgentRequest request, CancellationToken ct)
    {
        var agentExists = await db.Agents.AnyAsync(a => a.Id == request.AgentId && a.Enabled, ct);
        if (!agentExists)
        {
            return Problem(detail: "Agent 不存在或已停用", statusCode: 404);
        }

        try
        {
            var run = await executor.EnqueueForWorkItemAsync(request.AgentId, id, "assigned", ct);
            await queue.EnqueueAsync(run.Id);
            return Created($"/api/agent-runs/{run.Id}",
                new AgentRunListItemDto(
                    run.Id, string.Empty, run.WorkItemId, null, run.Trigger,
                    run.Status.ToString().ToLowerInvariant(), null,
                    run.CreatedAt, null, null, null, null));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
    }

    /// <summary>Agent 试运行（不关联工作项）。</summary>
    [HttpPost("api/agents/{id:guid}/runs")]
    public async Task<ActionResult<AgentRunListItemDto>> Preview(
        Guid id, [FromBody] PreviewAgentRequest request, CancellationToken ct)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == id && a.Enabled, ct);
        if (agent is null)
        {
            return Problem(detail: "Agent 不存在或已停用", statusCode: 404);
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Problem(detail: "提示词必填", statusCode: 400);
        }

        var run = new AgentRun
        {
            AgentId = agent.Id,
            Trigger = "preview",
            Prompt = request.Prompt.Trim(),
            Status = AgentRunStatus.Queued,
        };
        db.AgentRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(run.Id);

        return Created($"/api/agent-runs/{run.Id}",
            new AgentRunListItemDto(
                run.Id, agent.Name, null, null, run.Trigger, "queued", null,
                run.CreatedAt, null, null, null, null));
    }

    /// <summary>取消（接管）进行中的 AgentRun。</summary>
    [HttpPost("api/agent-runs/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var canceled = await executor.CancelAsync(id, ct);
        return canceled ? NoContent() : Problem(detail: "运行不存在或已结束", statusCode: 400);
    }
}
