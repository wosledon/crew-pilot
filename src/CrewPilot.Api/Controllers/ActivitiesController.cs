using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class ActivitiesController(CrewPilotDbContext db) : ControllerBase
{
    /// <summary>项目活动流（最近优先，内存排序以规避 SQLite 对 DateTimeOffset 排序的限制）。</summary>
    [HttpGet("api/projects/{projectId:guid}/activities")]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> List(
        Guid projectId, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var size = Math.Clamp(take, 1, 200);

        var rows = await db.Activities
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
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
