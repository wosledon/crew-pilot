namespace CrewPilot.Core.Providers;

using CrewPilot.Core.Common;

/// <summary>模型供应商：OpenAI 兼容端点，密钥经 ISecretProtector 加密存储。</summary>
public class ModelProvider : Entity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>OpenAI 兼容 BaseUrl，如 https://api.openai.com/v1。</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>加密后的 API key（DPAPI），永不出现在响应与日志。</summary>
    public string ApiKeyProtected { get; set; } = string.Empty;

    public ProviderSource Source { get; set; } = ProviderSource.Manual;

    public bool Enabled { get; set; } = true;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ModelCatalogEntry> Models { get; set; } = new List<ModelCatalogEntry>();
}
