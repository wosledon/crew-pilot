using System.Text.Json;

namespace CrewPilot.Infrastructure.Providers;

/// <summary>models.dev 目录解析结果条目。</summary>
public sealed record ParsedModel(
    string ProviderKey,
    string ModelId,
    string DisplayName,
    int? ContextLength,
    string? Capabilities,
    decimal? InputPricePerMillion,
    decimal? OutputPricePerMillion);

/// <summary>
/// models.dev 目录解析（纯函数，便于单测）：
/// 兼容 { "providerKey": { "name": ..., "models": { "modelId": { ... } } } } 结构。
/// </summary>
public static class ModelsDevCatalogParser
{
    public static IReadOnlyList<ParsedModel> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var results = new List<ParsedModel>();
        foreach (var providerProperty in root.EnumerateObject())
        {
            var providerKey = providerProperty.Name;
            if (providerProperty.Value.ValueKind != JsonValueKind.Object ||
                !providerProperty.Value.TryGetProperty("models", out var models) ||
                models.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var modelProperty in models.EnumerateObject())
            {
                var model = modelProperty.Value;
                var displayName = model.TryGetProperty("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString() ?? modelProperty.Name
                    : modelProperty.Name;

                int? contextLength = model.TryGetProperty("context", out var contextElement) &&
                    contextElement.ValueKind == JsonValueKind.Number &&
                    contextElement.TryGetInt32(out var context)
                    ? context
                    : null;

                string? capabilities = model.TryGetProperty("capabilities", out var capabilitiesElement) &&
                    capabilitiesElement.ValueKind == JsonValueKind.Array
                    ? capabilitiesElement.GetRawText()
                    : null;

                decimal? inputPrice = null;
                decimal? outputPrice = null;
                if (model.TryGetProperty("pricing", out var pricing) &&
                    pricing.ValueKind == JsonValueKind.Object)
                {
                    inputPrice = ReadPrice(pricing, "input");
                    outputPrice = ReadPrice(pricing, "output");
                }

                results.Add(new ParsedModel(
                    providerKey,
                    modelProperty.Name,
                    displayName,
                    contextLength,
                    capabilities,
                    inputPrice,
                    outputPrice));
            }
        }

        return results;
    }

    private static decimal? ReadPrice(JsonElement pricing, string propertyName) =>
        pricing.TryGetProperty(propertyName, out var element) &&
        element.ValueKind == JsonValueKind.Number &&
        element.TryGetDecimal(out var price)
            ? price
            : null;
}
