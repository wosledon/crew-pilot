using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class ActivitiesController(CrewPilotDbContext db) : ControllerBase
{
    /// <summary>项目活动流（最近优先），可按工作项 / 事件类型过滤。</summary>
    [HttpGet("api/projects/{projectId:guid}/activities")]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> List(
        Guid projectId,
        [FromQuery] Guid? workItemId,
        [FromQuery] string? eventType,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var size = Math.Clamp(take, 1, 200);

        var query = db.Activities
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId);
        if (workItemId.HasValue)
        {
            query = query.Where(a => a.WorkItemId == workItemId);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var wanted = eventType.Trim();
            query = query.Where(a => a.EventType == wanted);
        }

        var rows = await query
            .Take(200)
            .Select(a => new
            {
                a.Id,
                a.EventType,
                a.Payload,
                ActorName = a.Actor != null ? a.Actor.DisplayName : null,
                a.ActorKind,
                a.WorkItemId,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(a => a.CreatedAt)
            .Take(size)
            .Select(a => new ActivityDto(
                a.Id, a.EventType, a.Payload, a.ActorName,
                a.ActorKind?.ToString().ToLowerInvariant(), a.WorkItemId, a.CreatedAt))
            .ToList();
        return Ok(items);
    }
}
