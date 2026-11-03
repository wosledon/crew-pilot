namespace CrewPilot.Api.Contracts;

public sealed record ProviderListItemDto(
    Guid Id,
    string Name,
    string BaseUrl,
    string Source,
    bool Enabled,
    bool IsDefault,
    int ModelCount);

public sealed record ModelDto(
    Guid Id,
    string ModelId,
    string DisplayName,
    int? ContextLength,
    string? Capabilities,
    decimal? InputPricePerMillion,
    decimal? OutputPricePerMillion,
    bool Enabled);

public sealed record ProviderDetailDto(
    Guid Id,
    string Name,
    string BaseUrl,
    string Source,
    bool Enabled,
    bool IsDefault,
    IReadOnlyList<ModelDto> Models);

public sealed record CreateProviderRequest(string Name, string BaseUrl, string ApiKey);

public sealed record UpdateProviderRequest(string Name, string BaseUrl, string? ApiKey, bool Enabled);

public sealed record TestConnectionDto(bool Ok, string Message, int ModelCount);
