namespace CrewPilot.Core.Audit;

using CrewPilot.Core.Common;

/// <summary>审计日志：敏感操作留痕（删除、发布、密钥配置、审批策略等），不可在 UI 修改。</summary>
public class AuditLog : Entity
{
    public Guid? ProjectId { get; set; }

    /// <summary>操作类型，如 project.delete / integration.upsert / release.create。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>目标类型，如 project / workitem / agent / skill / mcp。</summary>
    public string TargetType { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    /// <summary>操作者描述（单用户产品默认 owner）。</summary>
    public string Actor { get; set; } = "owner";

    /// <summary>详情（JSON 或文本，禁止包含密钥）。</summary>
    public string? Detail { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
