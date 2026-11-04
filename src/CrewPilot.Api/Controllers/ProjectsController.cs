using CrewPilot.Api.Contracts;
using CrewPilot.Core.Projects;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectSummaryDto>>> List(CancellationToken ct)
    {
        var rows = await db.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.Key, p.Name, p.RepoUrl, p.Status, Count = p.WorkItems.Count, p.CreatedAt })
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectSummaryDto(
                p.Id, p.Key, p.Name, p.RepoUrl,
                p.Status.ToString().ToLowerInvariant(), p.Count, p.CreatedAt))
            .ToList();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await GetAsync(id, ct);
        return dto is null ? Problem(detail: "项目不存在", statusCode: 404) : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDetailDto>> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var key = (request.Key ?? string.Empty).Trim().ToUpperInvariant();
        if (key.Length is 0 or > 32)
        {
            return Problem(detail: "项目 Key 必填且不超过 32 个字符", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "项目名称必填", statusCode: 400);
        }

        if (await db.Projects.AnyAsync(p => p.Key == key, ct))
        {
            return Problem(detail: $"项目 Key {key} 已存在", statusCode: 409);
        }

        if (!TryNormalizeLocalPath(request.LocalPath, out var localPath, out var localPathError))
        {
            return Problem(detail: localPathError, statusCode: 400);
        }

        var project = new Project
        {
            Key = key,
            Name = request.Name.Trim(),
            Description = request.Description,
            RepoUrl = request.RepoUrl,
            LocalPath = localPath,
        };

        // 单用户产品：负责人本人默认作为 Owner 成员
        project.Members.Add(new ProjectMember
        {
            Project = project,
            Kind = MemberKind.User,
            Role = ProjectRole.Owner,
            DisplayName = "我",
        });

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        var dto = await GetAsync(project.Id, ct);
        return Created($"/api/projects/{project.Id}", dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDetailDto>> Update(
        Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "项目名称必填", statusCode: 400);
        }

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        // LocalPath 为 null 表示不修改，空字符串表示清空
        if (request.LocalPath is not null)
        {
            if (!TryNormalizeLocalPath(request.LocalPath, out var localPath, out var localPathError))
            {
                return Problem(detail: localPathError, statusCode: 400);
            }

            project.LocalPath = localPath;
        }

        project.Name = request.Name.Trim();
        project.Description = request.Description;
        project.RepoUrl = request.RepoUrl;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var dto = await GetAsync(id, ct);
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>校验并规范化项目本地工作目录（Agent 沙箱 / git worktree 的根）。</summary>
    private static bool TryNormalizeLocalPath(string? raw, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (raw is null)
        {
            return true;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        try
        {
            var full = Path.GetFullPath(trimmed);
            if (!Directory.Exists(full))
            {
                error = $"本地工作目录不存在：{full}";
                return false;
            }

            normalized = full;
            return true;
        }
        catch (Exception)
        {
            error = "本地工作目录路径非法";
            return false;
        }
    }

    private async Task<ProjectDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Key,
                p.Name,
                p.Description,
                p.RepoUrl,
                p.LocalPath,
                p.Status,
                Count = p.WorkItems.Count,
                p.CreatedAt,
                p.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new ProjectDetailDto(
                row.Id, row.Key, row.Name, row.Description, row.RepoUrl, row.LocalPath,
                row.Status.ToString().ToLowerInvariant(), row.Count, row.CreatedAt, row.UpdatedAt);
    }
}
