using System.Text.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Infrastructure.Persistence;
using CrewPilot.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>工作项附件：上传 / 下载 / 删除。文件落在本地附件目录，数据库只记元数据。</summary>
[ApiController]
public sealed class AttachmentsController(
    CrewPilotDbContext db,
    IAttachmentStorage storage) : ControllerBase
{
    [HttpGet("api/workitems/{workItemId:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> List(Guid workItemId, CancellationToken ct)
    {
        var exists = await db.WorkItems.AnyAsync(w => w.Id == workItemId, ct);
        if (!exists)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        var rows = await db.Attachments
            .AsNoTracking()
            .Where(a => a.WorkItemId == workItemId)
            .Select(a => new AttachmentDto(
                a.Id, a.WorkItemId, a.FileName, a.ContentType, a.Size, a.CreatedAt))
            .ToListAsync(ct);

        // SQLite 不支持 DateTimeOffset 排序，改为内存排序
        return Ok(rows.OrderByDescending(a => a.CreatedAt).ToList());
    }

    [HttpPost("api/workitems/{workItemId:guid}/attachments")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(
        Guid workItemId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "请选择要上传的文件", statusCode: 400);
        }

        if (file.Length > storage.MaxFileSize)
        {
            return Problem(
                detail: $"文件大小不能超过 {storage.MaxFileSize / 1024 / 1024} MB",
                statusCode: 400);
        }

        var workItem = await db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workItemId, ct);
        if (workItem is null)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        var originalName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalName))
        {
            originalName = "file";
        }

        var (relativePath, size) = await storage.SaveAsync(
            workItemId, originalName, file.OpenReadStream(), ct);

        var attachment = new Attachment
        {
            WorkItemId = workItemId,
            FileName = originalName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            Size = size,
            StoragePath = relativePath,
        };
        db.Attachments.Add(attachment);
        db.Activities.Add(new Activity
        {
            ProjectId = workItem.ProjectId,
            WorkItemId = workItemId,
            EventType = "attachment.added",
            Payload = JsonSerializer.Serialize(new { fileName = originalName, size }),
            ActorKind = MemberKind.User,
        });
        await db.SaveChangesAsync(ct);

        var dto = new AttachmentDto(
            attachment.Id, attachment.WorkItemId, attachment.FileName,
            attachment.ContentType, attachment.Size, attachment.CreatedAt);
        return Created($"/api/attachments/{attachment.Id}/download", dto);
    }

    [HttpGet("api/attachments/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var attachment = await db.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (attachment is null)
        {
            return Problem(detail: "附件不存在", statusCode: 404);
        }

        string fullPath;
        try
        {
            fullPath = storage.GetFullPath(attachment.StoragePath);
        }
        catch (InvalidOperationException)
        {
            return Problem(detail: "附件存储路径非法", statusCode: 404);
        }

        return !System.IO.File.Exists(fullPath)
            ? Problem(detail: "附件文件已丢失", statusCode: 404)
            : PhysicalFile(fullPath, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("api/attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var attachment = await db.Attachments
            .Include(a => a.WorkItem)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (attachment is null)
        {
            return Problem(detail: "附件不存在", statusCode: 404);
        }

        storage.DeleteFile(attachment.StoragePath);

        if (attachment.WorkItem is not null)
        {
            db.Activities.Add(new Activity
            {
                ProjectId = attachment.WorkItem.ProjectId,
                WorkItemId = attachment.WorkItemId,
                EventType = "attachment.deleted",
                Payload = JsonSerializer.Serialize(new { fileName = attachment.FileName }),
                ActorKind = MemberKind.User,
            });
        }

        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
