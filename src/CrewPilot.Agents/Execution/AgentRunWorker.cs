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
            catch (Exception ex)
            {
                logger.LogError(ex, "AgentRun {RunId} 执行失败", runId);
            }
        }
    }
}
