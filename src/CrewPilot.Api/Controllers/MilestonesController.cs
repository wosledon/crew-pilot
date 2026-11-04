using System.Text.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Iterations;
using CrewPilot.Core.Projects;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>里程碑：甘特图与版本计划的时间锚点，迭代可归属到里程碑。</summary>
[ApiController]
public sealed class MilestonesController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/milestones")]
    public async Task<ActionResult<IReadOnlyList<MilestoneDto>>> List(Guid projectId, CancellationToken ct)
    {
        var exists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!exists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var rows = await db.Milestones
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.Date)
            .Select(m => new MilestoneDto(
                m.Id, m.ProjectId, m.Name, m.Description, m.Date, m.Iterations.Count))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("api/projects/{projectId:guid}/milestones")]
    public async Task<ActionResult<MilestoneDto>> Create(
        Guid projectId, [FromBody] CreateMilestoneRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            return Problem(detail: "里程碑名称必填且不超过 200 个字符", statusCode: 400);
        }

        var exists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!exists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var milestone = new Milestone
        {
            ProjectId = projectId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Date = request.Date,
        };
        db.Milestones.Add(milestone);
        AddActivity(projectId, "milestone.created", new { milestone.Name, milestone.Date });
        await db.SaveChangesAsync(ct);

        return Created($"/api/milestones/{milestone.Id}",
            new MilestoneDto(
                milestone.Id, projectId, milestone.Name, milestone.Description,
                milestone.Date, 0));
    }

    [HttpPut("api/milestones/{id:guid}")]
    public async Task<ActionResult<MilestoneDto>> Update(
        Guid id, [FromBody] UpdateMilestoneRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            return Problem(detail: "里程碑名称必填且不超过 200 个字符", statusCode: 400);
        }

        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (milestone is null)
        {
            return Problem(detail: "里程碑不存在", statusCode: 404);
        }

        milestone.Name = request.Name.Trim();
        milestone.Description = request.Description;
        milestone.Date = request.Date;
        await db.SaveChangesAsync(ct);

        var count = await db.Iterations.CountAsync(i => i.MilestoneId == id, ct);
        return Ok(new MilestoneDto(
            milestone.Id, milestone.ProjectId, milestone.Name,
            milestone.Description, milestone.Date, count));
    }

    [HttpDelete("api/milestones/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (milestone is null)
        {
            return Problem(detail: "里程碑不存在", statusCode: 404);
        }

        db.Milestones.Remove(milestone);
        AddActivity(milestone.ProjectId, "milestone.deleted", new { milestone.Name });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private void AddActivity(Guid projectId, string eventType, object payload)
    {
        db.Activities.Add(new Activity
        {
            ProjectId = projectId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            ActorKind = MemberKind.User,
        });
    }
}
