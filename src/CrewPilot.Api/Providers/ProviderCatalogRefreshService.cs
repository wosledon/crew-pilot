using CrewPilot.Core.Providers;
using CrewPilot.Infrastructure.Persistence;
using CrewPilot.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Providers;

/// <summary>
/// models.dev 目录定时刷新：默认每 24 小时刷新一次已启用供应商的模型目录。
/// 启动后延迟 1 分钟再刷新，避免阻塞 API 冷启动；可通过 Providers:RefreshHours 配置（≤0 关闭）。
/// </summary>
public sealed class ProviderCatalogRefreshService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ProviderCatalogRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refreshHours = configuration.GetValue("Providers:RefreshHours", 24);
        if (refreshHours <= 0)
        {
            logger.LogInformation("models.dev 定时刷新已关闭（Providers:RefreshHours={Hours}）", refreshHours);
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(refreshHours));
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrewPilotDbContext>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var sync = new ModelsDevSyncService(httpClientFactory, db);

        var providers = await db.ModelProviders
            .Where(p => p.Enabled)
            .ToListAsync(ct);
        if (providers.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var provider in providers)
            {
                var count = await sync.SyncAsync(provider, null, ct);
                logger.LogInformation(
                    "models.dev 定时刷新完成：provider={Provider} models={Count}",
                    provider.Name, count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 网络失败不抛出：保留本地缓存，下一个周期再试
            logger.LogWarning(ex, "models.dev 定时刷新失败，保留本地缓存");
        }
    }
}
