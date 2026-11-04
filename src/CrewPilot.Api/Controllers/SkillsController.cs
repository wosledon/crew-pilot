using CrewPilot.Api.Contracts;
using CrewPilot.Core.Skills;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>Skills 管理：提示词模板 + 工具白名单 + 脚本目录，可装配给 Agent。</summary>
[ApiController]
public sealed class SkillsController(
    CrewPilotDbContext db,
    AuditLogger audit) : ControllerBase
{
    [HttpGet("api/skills")]
    public async Task<ActionResult<IReadOnlyList<SkillDto>>> List(CancellationToken ct)
    {
        var skills = await db.Skills
            .AsNoTracking()
            .Include(s => s.Agents)
            .ToListAsync(ct);
        return Ok(skills
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList());
    }

    [HttpGet("api/skills/{id:guid}")]
    public async Task<ActionResult<SkillDto>> Get(Guid id, CancellationToken ct)
    {
        var skill = await db.Skills
            .AsNoTracking()
            .Include(s => s.Agents)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        return skill is null
            ? Problem(detail: "Skill 不存在", statusCode: 404)
            : Ok(ToDto(skill));
    }

    [HttpPost("api/skills")]
    public async Task<ActionResult<SkillDto>> Create(
        [FromBody] CreateSkillRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.PromptContent))
        {
            return Problem(detail: "名称与提示词内容必填", statusCode: 400);
        }

        var skill = new Skill
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            PromptContent = request.PromptContent,
            Tools = request.Tools,
            ScriptPath = request.ScriptPath,
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);
        return Created($"/api/skills/{skill.Id}", ToDto(skill));
    }

    [HttpPut("api/skills/{id:guid}")]
    public async Task<ActionResult<SkillDto>> Update(
        Guid id, [FromBody] UpdateSkillRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.PromptContent))
        {
            return Problem(detail: "名称与提示词内容必填", statusCode: 400);
        }

        var skill = await db.Skills.Include(s => s.Agents).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (skill is null)
        {
            return Problem(detail: "Skill 不存在", statusCode: 404);
        }

        skill.Name = request.Name.Trim();
        skill.Description = request.Description;
        skill.PromptContent = request.PromptContent;
        skill.Tools = request.Tools;
        skill.ScriptPath = request.ScriptPath;
        skill.Enabled = request.Enabled;
        skill.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(skill));
    }

    [HttpDelete("api/skills/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (skill is null)
        {
            return Problem(detail: "Skill 不存在", statusCode: 404);
        }

        db.Skills.Remove(skill);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("skill.delete", "skill", id.ToString(), ct: ct);
        return NoContent();
    }

    private static SkillDto ToDto(Skill skill) => new(
        skill.Id,
        skill.Name,
        skill.Description,
        skill.PromptContent,
        skill.Tools,
        skill.ScriptPath,
        skill.Enabled,
        skill.Agents.Count,
        skill.CreatedAt,
        skill.UpdatedAt);
}
