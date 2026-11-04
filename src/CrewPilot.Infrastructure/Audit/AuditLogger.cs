using CrewPilot.Core.Audit;
using CrewPilot.Infrastructure.Persistence;

namespace CrewPilot.Infrastructure.Audit;

/// <summary>审计日志写入：敏感操作必须经此留痕，且不得写入密钥/Token 原文。</summary>
public sealed class AuditLogger(CrewPilotDbContext db)
{
    public async Task LogAsync(
        string action,
        string targetType,
        string? targetId = null,
        Guid? projectId = null,
        string? detail = null,
        CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ProjectId = projectId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Actor = "owner",
            Detail = detail,
        });
        await db.SaveChangesAsync(ct);
    }
}
