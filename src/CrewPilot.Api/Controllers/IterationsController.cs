using CrewPilot.Api.Contracts;
using CrewPilot.Core.Iterations;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class IterationsController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/iterations")]
    public async Task<ActionResult<IReadOnlyList<IterationListItemDto>>> List(
        Guid projectId, CancellationToken ct)
    {
        var rows = await db.Iterations
            .AsNoTracking()
            .Where(i => i.ProjectId == projectId)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.Goal,
                i.StartDate,
                i.EndDate,
                i.Status,
                Count = i.WorkItems.Count,
                Estimated = i.WorkItems.Sum(w => (decimal?)w.EstimatedHours) ?? 0m,
            })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var loggedRows = await db.WorkLogs
            .Where(l => l.WorkItem.IterationId != null && ids.Contains(l.WorkItem.IterationId.Value))
            .GroupBy(l => l.WorkItem.IterationId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Sum(l => l.Hours) })
            .ToListAsync(ct);
        var logged = loggedRows.ToDictionary(x => x.Id, x => x.Total);

        var items = rows
            .OrderBy(r => r.StartDate)
            .Select(r => new IterationListItemDto(
                r.Id, r.Name, r.Goal, r.StartDate, r.EndDate,
                r.Status.ToString().ToLowerInvariant(), r.Count, r.Estimated,
                logged.TryGetValue(r.Id, out var hours) ? hours : 0m))
            .ToList();
        return Ok(items);
    }

    [HttpGet("api/iterations/{id:guid}")]
    public async Task<ActionResult<IterationDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await db.Iterations
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id,
                i.ProjectId,
                i.Name,
                i.Goal,
                i.StartDate,
                i.EndDate,
                i.Status,
                Count = i.WorkItems.Count,
            })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? Problem(detail: "迭代不存在", statusCode: 404)
            : new IterationDetailDto(
                row.Id, row.ProjectId, row.Name, row.Goal, row.StartDate, row.EndDate,
                row.Status.ToString().ToLowerInvariant(), row.Count);
    }

    [HttpPost("api/projects/{projectId:guid}/iterations")]
    public async Task<ActionResult<IterationDetailDto>> Create(
        Guid projectId, [FromBody] CreateIterationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "迭代名称必填", statusCode: 400);
        }

        if (request.EndDate < request.StartDate)
        {
            return Problem(detail: "结束日期不能早于开始日期", statusCode: 400);
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var iteration = new Iteration
        {
            ProjectId = projectId,
            Name = request.Name.Trim(),
            Goal = request.Goal,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        };
        db.Iterations.Add(iteration);
        await db.SaveChangesAsync(ct);

        var dto = new IterationDetailDto(
            iteration.Id, projectId, iteration.Name, iteration.Goal,
            iteration.StartDate, iteration.EndDate, "planned", 0);
        return Created($"/api/iterations/{iteration.Id}", dto);
    }

    [HttpPut("api/iterations/{id:guid}")]
    public async Task<ActionResult<IterationDetailDto>> Update(
        Guid id, [FromBody] UpdateIterationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "迭代名称必填", statusCode: 400);
        }

        if (request.EndDate < request.StartDate)
        {
            return Problem(detail: "结束日期不能早于开始日期", statusCode: 400);
        }

        var iteration = await db.Iterations.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (iteration is null)
        {
            return Problem(detail: "迭代不存在", statusCode: 404);
        }

        iteration.Name = request.Name.Trim();
        iteration.Goal = request.Goal;
        iteration.StartDate = request.StartDate;
        iteration.EndDate = request.EndDate;
        await db.SaveChangesAsync(ct);

        var count = await db.WorkItems.CountAsync(w => w.IterationId == id, ct);
        return new IterationDetailDto(
            iteration.Id, iteration.ProjectId, iteration.Name, iteration.Goal,
            iteration.StartDate, iteration.EndDate,
            iteration.Status.ToString().ToLowerInvariant(), count);
    }

    [HttpDelete("api/iterations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var iteration = await db.Iterations.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (iteration is null)
        {
            return Problem(detail: "迭代不存在", statusCode: 404);
        }

        db.Iterations.Remove(iteration);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>开始迭代：planned → active。</summary>
    [HttpPost("api/iterations/{id:guid}/start")]
    public async Task<ActionResult<IterationDetailDto>> Start(Guid id, CancellationToken ct)
    {
        var iteration = await db.Iterations.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (iteration is null)
        {
            return Problem(detail: "迭代不存在", statusCode: 404);
        }

        if (iteration.Status != IterationStatus.Planned)
        {
            return Problem(detail: "只有计划中的迭代可以开始", statusCode: 400);
        }

        iteration.Status = IterationStatus.Active;
        await db.SaveChangesAsync(ct);

        var count = await db.WorkItems.CountAsync(w => w.IterationId == id, ct);
        return new IterationDetailDto(
            iteration.Id, iteration.ProjectId, iteration.Name, iteration.Goal,
            iteration.StartDate, iteration.EndDate, "active", count);
    }

    /// <summary>完成迭代：active → completed。</summary>
    [HttpPost("api/iterations/{id:guid}/complete")]
    public async Task<ActionResult<IterationDetailDto>> Complete(Guid id, CancellationToken ct)
    {
        var iteration = await db.Iterations.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (iteration is null)
        {
            return Problem(detail: "迭代不存在", statusCode: 404);
        }

        if (iteration.Status != IterationStatus.Active)
        {
            return Problem(detail: "只有进行中的迭代可以完成", statusCode: 400);
        }

        iteration.Status = IterationStatus.Completed;
        await db.SaveChangesAsync(ct);

        var count = await db.WorkItems.CountAsync(w => w.IterationId == id, ct);
        return new IterationDetailDto(
            iteration.Id, iteration.ProjectId, iteration.Name, iteration.Goal,
            iteration.StartDate, iteration.EndDate, "completed", count);
    }

    /// <summary>迭代燃尽图：按工时的剩余量 vs 理想线（1:1 折算，不加权）。</summary>
    [HttpGet("api/iterations/{id:guid}/burndown")]
    public async Task<ActionResult<BurndownDto>> Burndown(Guid id, CancellationToken ct)
    {
        var iteration = await db.Iterations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (iteration is null)
        {
            return Problem(detail: "迭代不存在", statusCode: 404);
        }

        var items = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.IterationId == id)
            .Select(w => new { w.Id, w.EstimatedHours })
            .ToListAsync(ct);
        var ids = items.Select(i => i.Id).ToList();

        var logs = ids.Count == 0
            ? []
            : await db.WorkLogs
                .AsNoTracking()
                .Where(l => ids.Contains(l.WorkItemId))
                .Select(l => new { l.Date, l.Hours })
                .ToListAsync(ct);

        var totalEstimate = items.Sum(i => i.EstimatedHours) ?? 0m;
        var byDate = logs
            .GroupBy(l => l.Date)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Hours));

        var points = new List<BurndownPointDto>();
        decimal cumulative = 0m;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var span = Math.Max(1, iteration.EndDate.DayNumber - iteration.StartDate.DayNumber);
        for (var date = iteration.StartDate;
             date <= iteration.EndDate && date <= today;
             date = date.AddDays(1))
        {
            if (byDate.TryGetValue(date, out var hours))
            {
                cumulative += hours;
            }

            var elapsed = date.DayNumber - iteration.StartDate.DayNumber;
            var ideal = totalEstimate * (1m - (decimal)elapsed / span);
            points.Add(new BurndownPointDto(
                date,
                Math.Max(0m, totalEstimate - cumulative),
                Math.Max(0m, ideal)));
        }

        return new BurndownDto(iteration.Id, totalEstimate, points);
    }
}
