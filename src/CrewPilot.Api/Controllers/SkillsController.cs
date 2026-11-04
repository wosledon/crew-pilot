using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Skills;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Persistence;
using CrewPilot.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>Skills 管理：提示词模板 + 工具白名单 + 脚本目录，可装配给 Agent。</summary>
[ApiController]
public sealed class SkillsController(
    CrewPilotDbContext db,
    SkillStorage storage,
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
        storage.Delete(id);
        await audit.LogAsync("skill.delete", "skill", id.ToString(), ct: ct);
        return NoContent();
    }

    /// <summary>导出为 SKILL.md（YAML front matter + 提示词正文）。</summary>
    [HttpGet("api/skills/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var skill = await db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (skill is null)
        {
            return Problem(detail: "Skill 不存在", statusCode: 404);
        }

        var markdown = ToSkillMarkdown(skill);
        var safeName = string.Concat(skill.Name.Select(
            ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return File(
            Encoding.UTF8.GetBytes(markdown),
            "text/markdown; charset=utf-8",
            $"{safeName}.SKILL.md");
    }

    /// <summary>从 SKILL.md 导入 Skill（同名已存在返回 409）。</summary>
    [HttpPost("api/skills/import")]
    public async Task<ActionResult<SkillDto>> Import(
        [FromBody] ImportSkillRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Problem(detail: "SKILL.md 内容必填", statusCode: 400);
        }

        var parsed = ParseSkillMarkdown(request.Content);
        if (parsed is null)
        {
            return Problem(
                detail: "SKILL.md 格式无效：需要 --- 包裹的 YAML front matter 与正文提示词",
                statusCode: 400);
        }

        var name = parsed.Value.Meta.GetValueOrDefault("name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            return Problem(detail: "front matter 缺少 name", statusCode: 400);
        }

        var exists = await db.Skills.AnyAsync(s => s.Name == name, ct);
        if (exists)
        {
            return Problem(detail: $"Skill {name} 已存在", statusCode: 409);
        }

        var skill = new Skill
        {
            Name = name,
            Description = parsed.Value.Meta.GetValueOrDefault("description"),
            PromptContent = parsed.Value.Body,
            Tools = NormalizeTools(parsed.Value.Meta.GetValueOrDefault("tools")),
            ScriptPath = parsed.Value.Meta.GetValueOrDefault("scriptPath"),
            Enabled = !string.Equals(
                parsed.Value.Meta.GetValueOrDefault("enabled"), "false",
                StringComparison.OrdinalIgnoreCase),
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("skill.import", "skill", skill.Id.ToString(), ct: ct);
        return Created($"/api/skills/{skill.Id}", ToDto(skill));
    }

    /// <summary>下载资源包 zip（SKILL.md + scripts/ + resources/）。</summary>
    [HttpGet("api/skills/{id:guid}/bundle")]
    public async Task<IActionResult> ExportBundle(Guid id, CancellationToken ct)
    {
        var skill = await db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (skill is null)
        {
            return Problem(detail: "Skill 不存在", statusCode: 404);
        }

        var bytes = storage.BuildBundle(id, ToSkillMarkdown(skill));
        var safeName = string.Concat(skill.Name.Select(
            ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return File(bytes, "application/zip", $"{safeName}.skill.zip");
    }

    /// <summary>Skill 资源目录中的文件列表（scripts/、resources/）。</summary>
    [HttpGet("api/skills/{id:guid}/files")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListFiles(
        Guid id, CancellationToken ct)
    {
        var exists = await db.Skills.AnyAsync(s => s.Id == id, ct);
        return exists
            ? Ok(storage.ListFiles(id))
            : Problem(detail: "Skill 不存在", statusCode: 404);
    }

    /// <summary>导入 Skill 资源包 zip：SKILL.md 定义元数据，scripts/resources 落盘。</summary>
    [HttpPost("api/skills/import-bundle")]
    [RequestSizeLimit(SkillStorage.MaxBundleBytes)]
    public async Task<ActionResult<SkillDto>> ImportBundle(
        [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "资源包文件必填", statusCode: 400);
        }

        if (file.Length > SkillStorage.MaxBundleBytes)
        {
            return Problem(detail: "资源包不能超过 10MB", statusCode: 400);
        }

        using var buffer = new MemoryStream();
        await using (var upload = file.OpenReadStream())
        {
            await upload.CopyToAsync(buffer, ct);
        }

        buffer.Position = 0;
        string? markdown = null;
        try
        {
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
            {
                var entry = archive.Entries.FirstOrDefault(e =>
                    string.Equals(
                        Path.GetFileName(e.FullName), "SKILL.md",
                        StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    markdown = await reader.ReadToEndAsync(ct);
                }
            }
        }
        catch (InvalidDataException)
        {
            return Problem(detail: "不是合法的 zip 资源包", statusCode: 400);
        }

        if (markdown is null)
        {
            return Problem(detail: "资源包缺少 SKILL.md", statusCode: 400);
        }

        var parsed = ParseSkillMarkdown(markdown);
        if (parsed is null)
        {
            return Problem(detail: "SKILL.md 格式无效", statusCode: 400);
        }

        var name = parsed.Value.Meta.GetValueOrDefault("name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            return Problem(detail: "front matter 缺少 name", statusCode: 400);
        }

        if (await db.Skills.AnyAsync(s => s.Name == name, ct))
        {
            return Problem(detail: $"Skill {name} 已存在", statusCode: 409);
        }

        var skill = new Skill
        {
            Name = name,
            Description = parsed.Value.Meta.GetValueOrDefault("description"),
            PromptContent = parsed.Value.Body,
            Tools = NormalizeTools(parsed.Value.Meta.GetValueOrDefault("tools")),
            ScriptPath = parsed.Value.Meta.GetValueOrDefault("scriptPath"),
            Enabled = !string.Equals(
                parsed.Value.Meta.GetValueOrDefault("enabled"), "false",
                StringComparison.OrdinalIgnoreCase),
        };

        try
        {
            buffer.Position = 0;
            await storage.ExtractAsync(skill.Id, buffer, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            storage.Delete(skill.Id);
            return Problem(detail: ex.Message, statusCode: 400);
        }

        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("skill.importBundle", "skill", skill.Id.ToString(), ct: ct);
        return Created($"/api/skills/{skill.Id}", ToDto(skill));
    }

    private static string ToSkillMarkdown(Skill skill)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"name: {skill.Name}");
        if (!string.IsNullOrWhiteSpace(skill.Description))
        {
            builder.AppendLine($"description: {SingleLine(skill.Description)}");
        }

        if (!string.IsNullOrWhiteSpace(skill.Tools))
        {
            builder.AppendLine($"tools: {skill.Tools}");
        }

        if (!string.IsNullOrWhiteSpace(skill.ScriptPath))
        {
            builder.AppendLine($"scriptPath: {skill.ScriptPath}");
        }

        builder.AppendLine($"enabled: {(skill.Enabled ? "true" : "false")}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(skill.PromptContent);
        return builder.ToString();
    }

    private static string SingleLine(string value) =>
        value.ReplaceLineEndings(" ").Trim();

    private static (Dictionary<string, string> Meta, string Body)? ParseSkillMarkdown(
        string content)
    {
        var normalized = content.ReplaceLineEndings("\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return null;
        }

        var closing = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (closing < 0)
        {
            return null;
        }

        var metaBlock = normalized[4..closing];
        var bodyStart = closing + "\n---".Length;
        var body = normalized[bodyStart..].TrimStart('\n').TrimEnd();
        if (body.Length == 0)
        {
            return null;
        }

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in metaBlock.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');
            if (key.Length > 0)
            {
                meta[key] = value;
            }
        }

        return (meta, body);
    }

    private static string? NormalizeTools(string? tools)
    {
        if (string.IsNullOrWhiteSpace(tools))
        {
            return null;
        }

        var value = tools.Trim();
        if (value.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                return document.RootElement.ValueKind == JsonValueKind.Array
                    ? document.RootElement.GetRawText()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        var list = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        return list.Count == 0 ? null : JsonSerializer.Serialize(list);
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
