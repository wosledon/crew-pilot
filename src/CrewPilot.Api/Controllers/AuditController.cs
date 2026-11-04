using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>审计日志查询（只读）。</summary>
[ApiController]
public sealed class AuditController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet("api/audit")]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> List(
        [FromQuery] Guid? projectId,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var size = Math.Clamp(take, 1, 500);
        var query = db.AuditLogs.AsNoTracking();
        if (projectId is Guid pid)
        {
            query = query.Where(a => a.ProjectId == pid);
        }

        var rows = await query.ToListAsync(ct);
        return Ok(rows
            .OrderByDescending(a => a.CreatedAt)
            .Take(size)
            .Select(a => new AuditLogDto(
                a.Id, a.ProjectId, a.Action, a.TargetType, a.TargetId,
                a.Actor, a.Detail, a.CreatedAt))
            .ToList());
    }
}
