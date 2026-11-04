using CrewPilot.Api.Contracts;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Providers;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Persistence;
using CrewPilot.Infrastructure.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class ProvidersController(
    CrewPilotDbContext db,
    ISecretProtector secrets,
    AuditLogger audit,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet("api/providers")]
    public async Task<ActionResult<IReadOnlyList<ProviderListItemDto>>> List(CancellationToken ct)
    {
        var rows = await db.ModelProviders
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.BaseUrl,
                p.Source,
                p.Enabled,
                p.IsDefault,
                Count = p.Models.Count,
            })
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .Select(p => new ProviderListItemDto(
                p.Id, p.Name, p.BaseUrl,
                p.Source.ToString().ToLowerInvariant(),
                p.Enabled, p.IsDefault, p.Count))
            .ToList();
        return Ok(items);
    }

    [HttpGet("api/providers/{id:guid}")]
    public async Task<ActionResult<ProviderDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var provider = await db.ModelProviders
            .AsNoTracking()
            .Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        return ToDto(provider);
    }

    [HttpPost("api/providers")]
    public async Task<ActionResult<ProviderDetailDto>> Create(
        [FromBody] CreateProviderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "供应商名称必填", statusCode: 400);
        }

        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out _))
        {
            return Problem(detail: "BaseUrl 必须是合法的绝对地址", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return Problem(detail: "API key 必填", statusCode: 400);
        }

        var normalizedUrl = request.BaseUrl.TrimEnd('/');
        if (await db.ModelProviders.AnyAsync(p => p.Name == request.Name.Trim(), ct))
        {
            return Problem(detail: "供应商名称已存在", statusCode: 409);
        }

        var isFirst = !await db.ModelProviders.AnyAsync(ct);
        var provider = new ModelProvider
        {
            Name = request.Name.Trim(),
            BaseUrl = normalizedUrl,
            ApiKeyProtected = secrets.Protect(request.ApiKey),
            IsDefault = isFirst,
        };
        db.ModelProviders.Add(provider);
        await db.SaveChangesAsync(ct);

        return Created($"/api/providers/{provider.Id}", ToDto(provider));
    }

    [HttpPut("api/providers/{id:guid}")]
    public async Task<ActionResult<ProviderDetailDto>> Update(
        Guid id, [FromBody] UpdateProviderRequest request, CancellationToken ct)
    {
        var provider = await db.ModelProviders
            .Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "供应商名称必填", statusCode: 400);
        }

        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out _))
        {
            return Problem(detail: "BaseUrl 必须是合法的绝对地址", statusCode: 400);
        }

        provider.Name = request.Name.Trim();
        provider.BaseUrl = request.BaseUrl.TrimEnd('/');
        provider.Enabled = request.Enabled;
        var rotatedKey = !string.IsNullOrWhiteSpace(request.ApiKey);
        if (rotatedKey)
        {
            provider.ApiKeyProtected = secrets.Protect(request.ApiKey!);
        }

        provider.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        if (rotatedKey)
        {
            await audit.LogAsync(
                "provider.keyRotated", "provider", provider.Id.ToString(), ct: ct);
        }

        return ToDto(provider);
    }

    [HttpDelete("api/providers/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var provider = await db.ModelProviders.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        db.ModelProviders.Remove(provider);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>连通性测试：请求 {BaseUrl}/models（Bearer 鉴权）。</summary>
    [HttpPost("api/providers/{id:guid}/test")]
    public async Task<ActionResult<TestConnectionDto>> Test(Guid id, CancellationToken ct)
    {
        var provider = await db.ModelProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        try
        {
            var apiKey = secrets.Unprotect(provider.ApiKeyProtected);
            var client = httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{provider.BaseUrl}/models");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var count = 0;
                try
                {
                    using var document = System.Text.Json.JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("data", out var data) &&
                        data.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        count = data.GetArrayLength();
                    }
                }
                catch
                {
                    // 响应体不可解析时仅报告连通
                }

                return new TestConnectionDto(true, "连接成功", count);
            }

            return new TestConnectionDto(false, $"HTTP {(int)response.StatusCode}", 0);
        }
        catch (Exception ex)
        {
            return new TestConnectionDto(false, ex.Message, 0);
        }
    }

    /// <summary>同步 models.dev 模型目录到本地缓存。</summary>
    [HttpPost("api/providers/{id:guid}/sync-models")]
    public async Task<ActionResult<object>> SyncModels(
        Guid id, [FromQuery] string? catalogKey, CancellationToken ct)
    {
        var provider = await db.ModelProviders.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        try
        {
            var sync = new ModelsDevSyncService(httpClientFactory, db);
            var count = await sync.SyncAsync(provider, catalogKey, ct);
            return Ok(new { synced = count });
        }
        catch (Exception ex)
        {
            return Problem(detail: $"同步失败：{ex.Message}", statusCode: 502);
        }
    }

    /// <summary>设为默认供应商（全局唯一；停用的供应商不可设为默认）。</summary>
    [HttpPut("api/providers/{id:guid}/default")]
    public async Task<ActionResult<ProviderDetailDto>> SetDefault(Guid id, CancellationToken ct)
    {
        var provider = await db.ModelProviders
            .Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        if (!provider.Enabled)
        {
            return Problem(detail: "停用的供应商不能设为默认", statusCode: 400);
        }

        var providers = await db.ModelProviders.ToListAsync(ct);
        foreach (var item in providers)
        {
            item.IsDefault = item.Id == id;
            if (item.IsDefault)
            {
                item.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(provider));
    }

    /// <summary>手动添加模型目录条目（覆盖 models.dev 同步结果）。</summary>
    [HttpPost("api/providers/{id:guid}/models")]
    public async Task<ActionResult<ModelDto>> AddModel(
        Guid id, [FromBody] UpsertModelRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Problem(detail: "模型标识必填", statusCode: 400);
        }

        if (request.InputPricePerMillion < 0 || request.OutputPricePerMillion < 0)
        {
            return Problem(detail: "价格不能为负", statusCode: 400);
        }

        var provider = await db.ModelProviders.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        var modelId = request.ModelId.Trim();
        if (await db.ModelCatalogEntries.AnyAsync(
                m => m.ProviderId == id && m.ModelId == modelId, ct))
        {
            return Problem(detail: $"模型 {modelId} 已存在", statusCode: 409);
        }

        var entry = new ModelCatalogEntry
        {
            ProviderId = id,
            ModelId = modelId,
            DisplayName = request.DisplayName?.Trim() ?? modelId,
            ContextLength = request.ContextLength,
            Capabilities = request.Capabilities,
            InputPricePerMillion = request.InputPricePerMillion,
            OutputPricePerMillion = request.OutputPricePerMillion,
            Enabled = request.Enabled ?? true,
        };
        db.ModelCatalogEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return Created($"/api/providers/models/{entry.Id}", ToModelDto(entry));
    }

    [HttpPut("api/providers/models/{modelId:guid}")]
    public async Task<ActionResult<ModelDto>> UpdateModel(
        Guid modelId, [FromBody] UpsertModelRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Problem(detail: "模型标识必填", statusCode: 400);
        }

        if (request.InputPricePerMillion < 0 || request.OutputPricePerMillion < 0)
        {
            return Problem(detail: "价格不能为负", statusCode: 400);
        }

        var entry = await db.ModelCatalogEntries.FirstOrDefaultAsync(m => m.Id == modelId, ct);
        if (entry is null)
        {
            return Problem(detail: "模型不存在", statusCode: 404);
        }

        entry.ModelId = request.ModelId.Trim();
        entry.DisplayName = request.DisplayName?.Trim() ?? entry.ModelId;
        entry.ContextLength = request.ContextLength;
        entry.Capabilities = request.Capabilities;
        entry.InputPricePerMillion = request.InputPricePerMillion;
        entry.OutputPricePerMillion = request.OutputPricePerMillion;
        entry.Enabled = request.Enabled ?? true;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ToModelDto(entry));
    }

    [HttpDelete("api/providers/models/{modelId:guid}")]
    public async Task<IActionResult> DeleteModel(Guid modelId, CancellationToken ct)
    {
        var entry = await db.ModelCatalogEntries.FirstOrDefaultAsync(m => m.Id == modelId, ct);
        if (entry is null)
        {
            return Problem(detail: "模型不存在", statusCode: 404);
        }

        db.ModelCatalogEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ProviderDetailDto ToDto(ModelProvider provider) => new(
        provider.Id,
        provider.Name,
        provider.BaseUrl,
        provider.Source.ToString().ToLowerInvariant(),
        provider.Enabled,
        provider.IsDefault,
        provider.Models
            .OrderBy(m => m.ModelId)
            .Select(ToModelDto)
            .ToList());

    private static ModelDto ToModelDto(ModelCatalogEntry model) => new(
        model.Id, model.ModelId, model.DisplayName, model.ContextLength, model.Capabilities,
        model.InputPricePerMillion, model.OutputPricePerMillion, model.Enabled);
}
