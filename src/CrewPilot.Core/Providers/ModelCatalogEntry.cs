namespace CrewPilot.Core.Providers;

using CrewPilot.Core.Common;

/// <summary>模型目录条目：来自 models.dev 同步或手动添加，本地缓存可覆盖。</summary>
public class ModelCatalogEntry : Entity
{
    public Guid ProviderId { get; set; }

    public ModelProvider Provider { get; set; } = null!;

    /// <summary>模型标识，如 gpt-4o-mini。</summary>
    public string ModelId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int? ContextLength { get; set; }

    /// <summary>能力标记（JSON 数组，如 ["tools","vision"]）。</summary>
    public string? Capabilities { get; set; }

    /// <summary>每百万 token 输入价格（美元，可选）。</summary>
    public decimal? InputPricePerMillion { get; set; }

    /// <summary>每百万 token 输出价格（美元，可选）。</summary>
    public decimal? OutputPricePerMillion { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
