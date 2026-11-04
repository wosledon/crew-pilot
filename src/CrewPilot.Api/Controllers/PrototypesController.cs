using CrewPilot.Api.Contracts;
using CrewPilot.Core.Prototypes;
using CrewPilot.Infrastructure.Persistence;
using CrewPilot.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>
/// 原型工坊：HTML / URL / 图片 / MCP 引用；保存自动生成版本快照，可恢复。
/// 图片落本地附件存储，经 /api/prototype-images 只读访问。
/// </summary>
[ApiController]
public sealed class PrototypesController(
    CrewPilotDbContext db,
    IAttachmentStorage storage) : ControllerBase
{
    private static readonly string[] AllowedImageExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif"];

    private const long MaxImageBytes = 10 * 1024 * 1024;

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
            .Select(p => new
            {
                p.Id, p.ProjectId, p.RequirementId, p.Name, p.Type,
                p.Content, p.Version, p.CreatedByName, p.UpdatedAt,
            })
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new PrototypeDto(
                p.Id, p.ProjectId, p.RequirementId, p.Name,
                p.Type.ToString().ToLowerInvariant(), p.Content, p.Version,
                p.CreatedByName, p.UpdatedAt))
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

        var contentError = ValidateContent(type, request.Content);
        if (contentError is not null)
        {
            return Problem(detail: contentError, statusCode: 400);
        }

        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        if (request.RequirementId.HasValue &&
            !await db.Requirements.AnyAsync(
                r => r.Id == request.RequirementId.Value && r.ProjectId == projectId, ct))
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
        db.PrototypeVersions.Add(NewVersion(prototype));
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

        var contentError = ValidateContent(prototype.Type, request.Content);
        if (contentError is not null)
        {
            return Problem(detail: contentError, statusCode: 400);
        }

        if (request.RequirementId.HasValue &&
            !await db.Requirements.AnyAsync(
                r => r.Id == request.RequirementId.Value && r.ProjectId == prototype.ProjectId, ct))
        {
            return Problem(detail: "关联的需求不存在", statusCode: 400);
        }

        prototype.Name = request.Name.Trim();
        prototype.Content = request.Content;
        prototype.RequirementId = request.RequirementId;
        prototype.Version += 1;
        prototype.UpdatedAt = DateTimeOffset.UtcNow;
        db.PrototypeVersions.Add(NewVersion(prototype));
        await db.SaveChangesAsync(ct);

        return ToDto(prototype);
    }

    [HttpGet("api/prototypes/{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<PrototypeVersionDto>>> ListVersions(
        Guid id, CancellationToken ct)
    {
        if (!await db.Prototypes.AnyAsync(p => p.Id == id, ct))
        {
            return Problem(detail: "原型不存在", statusCode: 404);
        }

        var rows = await db.PrototypeVersions
            .AsNoTracking()
            .Where(v => v.PrototypeId == id)
            .Select(v => new PrototypeVersionDto(
                v.Id, v.PrototypeId, v.Version, v.Name, v.Content,
                v.CreatedByName, v.CreatedAt))
            .ToListAsync(ct);
        return Ok(rows.OrderByDescending(v => v.Version).ToList());
    }

    /// <summary>恢复历史版本：内容作为新版本写回（不丢失后续历史）。</summary>
    [HttpPost("api/prototypes/{id:guid}/versions/{version:int}/restore")]
    public async Task<ActionResult<PrototypeDto>> RestoreVersion(
        Guid id, int version, CancellationToken ct)
    {
        var prototype = await db.Prototypes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prototype is null)
        {
            return Problem(detail: "原型不存在", statusCode: 404);
        }

        var snapshot = await db.PrototypeVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.PrototypeId == id && v.Version == version, ct);
        if (snapshot is null)
        {
            return Problem(detail: "版本不存在", statusCode: 404);
        }

        prototype.Name = snapshot.Name;
        prototype.Content = snapshot.Content;
        prototype.Version += 1;
        prototype.UpdatedAt = DateTimeOffset.UtcNow;
        db.PrototypeVersions.Add(NewVersion(prototype, $"恢复自 v{version}"));
        await db.SaveChangesAsync(ct);

        return ToDto(prototype);
    }

    [HttpPost("api/projects/{projectId:guid}/prototype-images")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<PrototypeImageDto>> UploadImage(
        Guid projectId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "请选择要上传的图片", statusCode: 400);
        }

        if (file.Length > MaxImageBytes)
        {
            return Problem(detail: "图片大小不能超过 10 MB", statusCode: 400);
        }

        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            return Problem(detail: "仅支持 PNG / JPG / WEBP / GIF 图片", statusCode: 400);
        }

        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var (relativePath, size) = await storage.SaveAsync(
            projectId, originalName, file.OpenReadStream(), ct);
        var urlPath = relativePath.Replace('\\', '/');
        return Created(
            $"/api/prototype-images/{urlPath}",
            new PrototypeImageDto(relativePath, $"/api/prototype-images/{urlPath}", size));
    }

    [HttpGet("api/prototype-images/{**path}")]
    public IActionResult GetImage(string path)
    {
        string fullPath;
        try
        {
            fullPath = storage.GetFullPath(path);
        }
        catch (InvalidOperationException)
        {
            return Problem(detail: "图片路径非法", statusCode: 404);
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return Problem(detail: "图片不存在", statusCode: 404);
        }

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
        Response.Headers.CacheControl = "public,max-age=86400";
        return PhysicalFile(fullPath, contentType);
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

    private static string? ValidateContent(PrototypeType type, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "原型内容不能为空";
        }

        switch (type)
        {
            case PrototypeType.Url when
                !Uri.TryCreate(content, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps):
                return "URL 原型必须是 http(s) 绝对地址";
            case PrototypeType.Image when !AllowedImageExtensions.Contains(
                Path.GetExtension(content).ToLowerInvariant()):
                return "图片原型内容必须是已上传的图片路径（png/jpg/webp/gif）";
            default:
                return null;
        }
    }

    private static PrototypeVersion NewVersion(Prototype prototype, string? note = null) => new()
    {
        PrototypeId = prototype.Id,
        Version = prototype.Version,
        Name = prototype.Name,
        Content = prototype.Content,
        CreatedByName = note,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static PrototypeDto ToDto(Prototype p) => new(
        p.Id, p.ProjectId, p.RequirementId, p.Name,
        p.Type.ToString().ToLowerInvariant(), p.Content, p.Version,
        p.CreatedByName, p.UpdatedAt);
}
