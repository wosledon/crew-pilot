using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class ReportsController(CrewPilotDbContext db) : ControllerBase
{
    /// <summary>项目工时报表：总计 + 按类型/按指派人分组（预估 vs 已耗）。</summary>
    [HttpGet("api/projects/{projectId:guid}/hours-report")]
    public async Task<ActionResult<HoursReportDto>> HoursReport(Guid projectId, CancellationToken ct)
    {
        var items = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .Select(w => new
            {
                w.Id,
                Type = EF.Property<string>(w, "WorkItemType"),
                w.EstimatedHours,
                AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
            })
            .ToListAsync(ct);

        var ids = items.Select(i => i.Id).ToList();
        var loggedRows = ids.Count == 0
            ? []
            : await db.WorkLogs
                .AsNoTracking()
                .Where(l => ids.Contains(l.WorkItemId))
                .GroupBy(l => l.WorkItemId)
                .Select(g => new { Id = g.Key, Total = g.Sum(l => l.Hours) })
                .ToListAsync(ct);
        var logged = loggedRows.ToDictionary(x => x.Id, x => x.Total);

        decimal LoggedOf(Guid id) => logged.TryGetValue(id, out var hours) ? hours : 0m;

        var byType = items
            .GroupBy(i => i.Type)
            .Select(g => new HoursByGroupDto(
                g.Key,
                g.Key,
                g.Sum(i => i.EstimatedHours) ?? 0m,
                g.Sum(i => LoggedOf(i.Id))))
            .OrderBy(g => g.Key)
            .ToList();

        var byAssignee = items
            .GroupBy(i => i.AssigneeName ?? "unassigned")
            .Select(g => new HoursByGroupDto(
                g.Key,
                g.Key == "unassigned" ? "未指派" : g.Key,
                g.Sum(i => i.EstimatedHours) ?? 0m,
                g.Sum(i => LoggedOf(i.Id))))
            .OrderByDescending(g => g.LoggedHours)
            .ToList();

        return new HoursReportDto(
            items.Sum(i => i.EstimatedHours) ?? 0m,
            items.Sum(i => LoggedOf(i.Id)),
            byType,
            byAssignee);
    }
}
