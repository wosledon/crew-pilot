using CrewPilot.Core.Providers;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Http;

namespace CrewPilot.Infrastructure.Providers;

/// <summary>
/// models.dev 目录同步：拉取远程目录，与供应商本地缓存合并（手动覆盖优先）。
/// </summary>
public sealed class ModelsDevSyncService(
    IHttpClientFactory httpClientFactory,
    CrewPilotDbContext db)
{
    public const string CatalogUrl = "https://models.dev/api.json";

    /// <summary>同步指定供应商的模型目录，返回入库条目数。</summary>
    public async Task<int> SyncAsync(ModelProvider provider, string? catalogKey, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("models.dev");
        var json = await client.GetStringAsync(CatalogUrl, ct);
        var parsed = ModelsDevCatalogParser.Parse(json);

        var key = catalogKey ?? DeriveKey(provider.BaseUrl);
        var models = parsed
            .Where(m => string.Equals(m.ProviderKey, key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var existing = await db.ModelCatalogEntries
            .Where(m => m.ProviderId == provider.Id)
            .ToListAsync(ct);
        var byModelId = existing.ToDictionary(m => m.ModelId, StringComparer.OrdinalIgnoreCase);

        foreach (var model in models)
        {
            if (byModelId.TryGetValue(model.ModelId, out var entry))
            {
                entry.DisplayName = model.DisplayName;
                entry.ContextLength = model.ContextLength;
                entry.Capabilities = model.Capabilities;
                entry.InputPricePerMillion = model.InputPricePerMillion;
                entry.OutputPricePerMillion = model.OutputPricePerMillion;
                entry.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                db.ModelCatalogEntries.Add(new ModelCatalogEntry
                {
                    ProviderId = provider.Id,
                    ModelId = model.ModelId,
                    DisplayName = model.DisplayName,
                    ContextLength = model.ContextLength,
                    Capabilities = model.Capabilities,
                    InputPricePerMillion = model.InputPricePerMillion,
                    OutputPricePerMillion = model.OutputPricePerMillion,
                });
            }
        }

        provider.Source = ProviderSource.ModelsDev;
        provider.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return models.Count;
    }

    /// <summary>从 BaseUrl 推导目录 key：api.deepseek.com → deepseek。</summary>
    public static string? DeriveKey(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        var labels = host.Split('.');
        if (labels.Length >= 2)
        {
            var candidate = labels[^2];
            return candidate is "api" or "www" && labels.Length >= 3 ? labels[^3] : candidate;
        }

        return host;
    }
}
