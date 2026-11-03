using CrewPilot.Api.Contracts;
using CrewPilot.Core.Prototypes;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class PrototypesController(CrewPilotDbContext db) : ControllerBase
{
    /// <summary>原型列表（可按关联需求过滤）。</summary>
    [HttpGet("api/projects/{projectId:guid}/prototypes")]
    public async Task<ActionResult<IReadOnlyList<PrototypeDto>>> List(
        Guid projectId, [FromQuery] Guid? requirementId, CancellationToken ct)
    {
        var query = db.Prototypes
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId);
        if (requirementId.HasValue)
        {
            query = query.Where(p => p.RequirementId == requirementId);
        }

        var rows = await query
            .Select(p => new { p.Id, p.ProjectId, p.RequirementId, p.Name, p.Type, p.Content, p.Version, p.CreatedByName, p.UpdatedAt })
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => ToDto(p.Id, p.ProjectId, p.RequirementId, p.Name, p.Type, p.Content, p.Version, p.CreatedByName, p.UpdatedAt))
            .ToList();
        return Ok(items);
    }

    [HttpGet("api/prototypes/{id:guid}")]
    public async Task<ActionResult<PrototypeDto>> Get(Guid id, CancellationToken ct)
    {
        var prototype = await db.Prototypes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return prototype is null
            ? Problem(detail: "原型不存在", statusCode: 404)
            : ToDto(prototype);
    }

    [HttpPost("api/projects/{projectId:guid}/prototypes")]
    public async Task<ActionResult<PrototypeDto>> Create(
        Guid projectId, [FromBody] CreatePrototypeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "原型名称必填", statusCode: 400);
        }

        if (!Enum.TryParse<PrototypeType>(request.Type, ignoreCase: true, out var type))
        {
            return Problem(detail: $"不支持的原型类型 {request.Type}（html/url/image/mcp）", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Problem(detail: "原型内容不能为空", statusCode: 400);
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        if (request.RequirementId.HasValue &&
            !await db.Requirements.AnyAsync(r => r.Id == request.RequirementId.Value, ct))
        {
            return Problem(detail: "关联的需求不存在", statusCode: 400);
        }

        var prototype = new Prototype
        {
            ProjectId = projectId,
            RequirementId = request.RequirementId,
            Name = request.Name.Trim(),
            Type = type,
            Content = request.Content,
        };
        db.Prototypes.Add(prototype);
        await db.SaveChangesAsync(ct);

        return Created($"/api/prototypes/{prototype.Id}", ToDto(prototype));
    }

    [HttpPut("api/prototypes/{id:guid}")]
    public async Task<ActionResult<PrototypeDto>> Update(
        Guid id, [FromBody] UpdatePrototypeRequest request, CancellationToken ct)
    {
        var prototype = await db.Prototypes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prototype is null)
        {
            return Problem(detail: "原型不存在", statusCode: 404);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "原型名称必填", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Problem(detail: "原型内容不能为空", statusCode: 400);
        }

        prototype.Name = request.Name.Trim();
        prototype.Content = request.Content;
        prototype.Version += 1;
        prototype.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(prototype);
    }

    [HttpDelete("api/prototypes/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var prototype = await db.Prototypes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prototype is null)
        {
            return Problem(detail: "原型不存在", statusCode: 404);
        }

        db.Prototypes.Remove(prototype);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static PrototypeDto ToDto(Prototype p) =>
        new(p.Id, p.ProjectId, p.RequirementId, p.Name,
            p.Type.ToString().ToLowerInvariant(), p.Content, p.Version, p.CreatedByName, p.UpdatedAt);

    private static PrototypeDto ToDto(
        Guid id, Guid projectId, Guid? requirementId, string name,
        PrototypeType type, string content, int version, string? createdByName, DateTimeOffset updatedAt) =>
        new(id, projectId, requirementId, name,
            type.ToString().ToLowerInvariant(), content, version, createdByName, updatedAt);
}
