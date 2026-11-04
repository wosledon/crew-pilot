using CrewPilot.Api.Contracts;
using CrewPilot.Agents.Execution;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Services;
using CrewPilot.Core.WorkItems;
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

    /// <summary>Agent 试运行（可选关联工作项，注入需求上下文）。</summary>
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

        var workItem = request.WorkItemId is Guid workItemId
            ? await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct)
            : null;
        if (request.WorkItemId is not null && workItem is null)
        {
            return Problem(detail: "关联工作项不存在", statusCode: 404);
        }

        var run = new AgentRun
        {
            AgentId = agent.Id,
            WorkItemId = workItem?.Id,
            Trigger = "preview",
            Prompt = BuildPreviewPrompt(request.Prompt.Trim(), workItem),
            Status = AgentRunStatus.Queued,
        };
        db.AgentRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(run.Id);

        return Created($"/api/agent-runs/{run.Id}",
            new AgentRunListItemDto(
                run.Id, agent.Name, run.WorkItemId, workItem?.Title, run.Trigger, "queued", null,
                run.CreatedAt, null, null, null, null));
    }

    private static string BuildPreviewPrompt(string prompt, WorkItem? workItem)
    {
        if (workItem is null)
        {
            return prompt;
        }

        var builder = new System.Text.StringBuilder(prompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## 工作项上下文");
        builder.AppendLine($"标题：{workItem.Title}");
        builder.AppendLine($"类型：{ExecutorPromptType(workItem)}");
        builder.AppendLine($"状态：{ExecutorPromptStatus(workItem)}");
        if (!string.IsNullOrWhiteSpace(workItem.Description))
        {
            builder.AppendLine($"描述：{workItem.Description}");
        }

        if (!string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
        {
            builder.AppendLine($"验收标准：{workItem.AcceptanceCriteria}");
        }

        return builder.ToString();
    }

    private static string ExecutorPromptType(WorkItem workItem) => workItem switch
    {
        Requirement => "requirement",
        Defect => "defect",
        TaskItem => "task",
        _ => "workitem",
    };

    private static string ExecutorPromptStatus(WorkItem workItem) => workItem switch
    {
        Requirement requirement => requirement.Status.ToString(),
        Defect defect => defect.Status.ToString(),
        TaskItem task => WorkItemStateMachine.ToStatus(task),
        _ => string.Empty,
    };

    /// <summary>取消（接管）进行中的 AgentRun。</summary>
    [HttpPost("api/agent-runs/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var canceled = await executor.CancelAsync(id, ct);
        return canceled ? NoContent() : Problem(detail: "运行不存在或已结束", statusCode: 400);
    }
}
