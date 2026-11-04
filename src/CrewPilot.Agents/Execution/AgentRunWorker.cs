using CrewPilot.Core.Agents;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrewPilot.Agents.Execution;

/// <summary>AgentRun 后台执行 Worker：从队列取出运行并驱动执行器。</summary>
public sealed class AgentRunWorker(
    AgentRunQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AgentRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<AgentRunExecutor>();
            try
            {
                await executor.ExecuteAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 宿主停止：不再处理后续队列
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AgentRun {RunId} 执行失败", runId);
                await TryMarkFailedAsync(runId, ex.Message);
            }
        }
    }

    /// <summary>执行器抛出未捕获异常时兜底落 Failed，避免运行永久停在 Running。</summary>
    private async Task TryMarkFailedAsync(Guid runId, string message)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrewPilotDbContext>();
            var run = await db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId);
            if (run is null || run.Status is AgentRunStatus.Succeeded or AgentRunStatus.Failed or AgentRunStatus.Canceled)
            {
                return;
            }

            run.Status = AgentRunStatus.Failed;
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.Error = $"执行器异常：{message}";
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentRun {RunId} 兜底置为 Failed 失败", runId);
        }
    }
}
