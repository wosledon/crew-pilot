using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Iterations;
using CrewPilot.Core.Projects;
using CrewPilot.Core.WorkItems;
using CrewPilot.Core.Workflows;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>
/// 驾驶舱：跨项目的只读聚合视图。单用户产品的"看而不等"入口——
/// 卡点队列、我的待办、进行中的 AgentRun、项目进度与最近活动集中在一处。
/// </summary>
[ApiController]
public sealed class DashboardController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet("api/dashboard")]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] int take = 20, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var projectRows = await db.Projects
            .AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Key, p.Name, p.Status })
            .ToListAsync(ct);
        var projectIds = projectRows.Select(p => p.Id).ToList();

        var requirementStats = await db.Requirements
            .AsNoTracking()
            .Where(w => projectIds.Contains(w.ProjectId))
            .GroupBy(w => w.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Total = g.Count(),
                Done = g.Count(w => w.Status == RequirementStatus.Done),
            })
            .ToListAsync(ct);

        var defectStats = await db.Defects
            .AsNoTracking()
            .Where(w => projectIds.Contains(w.ProjectId))
            .GroupBy(w => w.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Total = g.Count(),
                Done = g.Count(w => w.Status == DefectStatus.Closed),
            })
            .ToListAsync(ct);

        var taskStats = await db.TaskItems
            .AsNoTracking()
            .Where(w => projectIds.Contains(w.ProjectId))
            .GroupBy(w => w.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Total = g.Count(),
                Done = g.Count(w => w.IsDone),
            })
            .ToListAsync(ct);

        var activeIterations = await db.Iterations
            .AsNoTracking()
            .Where(i => projectIds.Contains(i.ProjectId) && i.Status == IterationStatus.Active)
            .Select(i => new { i.ProjectId, i.Name, i.EndDate })
            .ToListAsync(ct);

        var statsByProject = requirementStats
            .Concat(defectStats)
            .Concat(taskStats)
            .GroupBy(s => s.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => (Total: g.Sum(x => x.Total), Done: g.Sum(x => x.Done)));

        var projects = projectRows.Select(p =>
        {
            var stat = statsByProject.TryGetValue(p.Id, out var s) ? s : (Total: 0, Done: 0);
            var iteration = activeIterations.FirstOrDefault(i => i.ProjectId == p.Id);
            var progress = stat.Total == 0 ? 0d : Math.Round(stat.Done * 100d / stat.Total, 1);
            return new DashboardProjectDto(
                p.Id, p.Key, p.Name, p.Status.ToString().ToLowerInvariant(),
                stat.Total, stat.Done, progress, iteration?.Name, iteration?.EndDate);
        }).ToList();

        var blockerRows = await db.Blockers
            .AsNoTracking()
            .Where(b => b.Status == BlockerStatus.Open)
            .Select(b => new
            {
                b.Id,
                b.ProjectId,
                ProjectName = b.WorkItem.Project.Name,
                b.WorkItemId,
                WorkItemTitle = b.WorkItem.Title,
                b.Kind,
                b.Reason,
                b.ContextSnapshot,
                b.CreatedAt,
            })
            .ToListAsync(ct);
        var blockers = blockerRows
            .OrderByDescending(b => b.CreatedAt)
            .Take(take)
            .Select(b => new DashboardBlockerDto(
                b.Id, b.ProjectId, b.ProjectName, b.WorkItemId, b.WorkItemTitle,
                b.Kind.ToString().ToLowerInvariant(), b.Reason, b.ContextSnapshot, b.CreatedAt))
            .ToList();

        // 我的待办 = 指派给人类成员（单用户产品的 Owner）且未完结的工作项
        var requirementTodos = await db.Requirements
            .AsNoTracking()
            .Where(w => w.Assignee != null && w.Assignee.Kind == MemberKind.User && w.Assignee.Role == ProjectRole.Owner &&
                        w.Status != RequirementStatus.Done && w.Status != RequirementStatus.Rejected)
            .Select(w => new
            {
                w.Id,
                w.ProjectId,
                ProjectName = w.Project.Name,
                w.Title,
                w.Status,
                w.Priority,
                w.DueDate,
                w.RemainingHours,
            })
            .ToListAsync(ct);

        var defectTodos = await db.Defects
            .AsNoTracking()
            .Where(w => w.Assignee != null && w.Assignee.Kind == MemberKind.User && w.Assignee.Role == ProjectRole.Owner &&
                        w.Status != DefectStatus.Closed && w.Status != DefectStatus.NotADefect &&
                        w.Status != DefectStatus.Deferred)
            .Select(w => new
            {
                w.Id,
                w.ProjectId,
                ProjectName = w.Project.Name,
                w.Title,
                w.Status,
                w.Priority,
                w.DueDate,
                w.RemainingHours,
            })
            .ToListAsync(ct);

        var taskTodos = await db.TaskItems
            .AsNoTracking()
            .Where(w => w.Assignee != null && w.Assignee.Kind == MemberKind.User && w.Assignee.Role == ProjectRole.Owner && !w.IsDone)
            .Select(w => new
            {
                w.Id,
                w.ProjectId,
                ProjectName = w.Project.Name,
                w.Title,
                w.Priority,
                w.DueDate,
                w.RemainingHours,
            })
            .ToListAsync(ct);

        var todos = requirementTodos
            .Select(w => new DashboardTodoDto(
                w.Id, w.ProjectId, w.ProjectName, w.Title, "requirement",
                w.Status.ToString().ToLowerInvariant(), w.Priority.ToString().ToLowerInvariant(),
                w.DueDate, w.RemainingHours, w.DueDate.HasValue && w.DueDate.Value < today))
            .Concat(defectTodos.Select(w => new DashboardTodoDto(
                w.Id, w.ProjectId, w.ProjectName, w.Title, "defect",
                w.Status.ToString().ToLowerInvariant(), w.Priority.ToString().ToLowerInvariant(),
                w.DueDate, w.RemainingHours, w.DueDate.HasValue && w.DueDate.Value < today)))
            .Concat(taskTodos.Select(w => new DashboardTodoDto(
                w.Id, w.ProjectId, w.ProjectName, w.Title, "task", "todo",
                w.Priority.ToString().ToLowerInvariant(),
                w.DueDate, w.RemainingHours, w.DueDate.HasValue && w.DueDate.Value < today)))
            .OrderBy(w => w.Priority)
            .ThenBy(w => w.DueDate ?? DateOnly.MaxValue)
            .Take(take)
            .ToList();

        var activeRunRows = await db.AgentRuns
            .AsNoTracking()
            .Where(r => r.Status == AgentRunStatus.Queued ||
                        r.Status == AgentRunStatus.Running ||
                        r.Status == AgentRunStatus.WaitingForHuman)
            .Select(r => new
            {
                r.Id,
                AgentName = r.Agent.Name,
                ProjectId = r.WorkItem != null
                    ? r.WorkItem.ProjectId
                    : r.Prototype != null
                        ? r.Prototype.ProjectId
                        : (Guid?)null,
                ProjectName = r.WorkItem != null
                    ? r.WorkItem.Project.Name
                    : r.Prototype != null
                        ? r.Prototype.Project.Name
                        : null,
                WorkItemTitle = r.WorkItem != null
                    ? r.WorkItem.Title
                    : r.Prototype != null
                        ? r.Prototype.Name
                        : null,
                r.Status,
                r.Trigger,
                r.CreatedAt,
            })
            .ToListAsync(ct);
        var activeRuns = activeRunRows
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => new DashboardRunDto(
                r.Id, r.AgentName, r.ProjectId, r.ProjectName, r.WorkItemTitle,
                r.Status.ToString().ToLowerInvariant(), r.Trigger, r.CreatedAt))
            .ToList();

        var activityRows = await db.Activities
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.ProjectId,
                ProjectName = a.Project != null ? a.Project.Name : null,
                a.EventType,
                a.Payload,
                ActorName = a.Actor != null ? a.Actor.DisplayName : null,
                a.ActorKind,
                a.WorkItemId,
                a.CreatedAt,
            })
            .ToListAsync(ct);
        var activities = activityRows
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new DashboardActivityDto(
                a.Id, a.ProjectId, a.ProjectName, a.EventType, a.Payload, a.ActorName,
                a.ActorKind?.ToString().ToLowerInvariant(), a.WorkItemId, a.CreatedAt))
            .ToList();

        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));
        var weekHours = await db.WorkLogs
            .AsNoTracking()
            .Where(w => w.Date >= weekStart)
            .SumAsync(w => (decimal?)w.Hours, ct) ?? 0m;

        var stats = new DashboardStatsDto(
            projects.Count, blockers.Count, activeRuns.Count, todos.Count, weekHours);

        return Ok(new DashboardDto(stats, projects, blockers, todos, activeRuns, activities));
    }
}
