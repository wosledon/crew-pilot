using System.Collections.Concurrent;

namespace CrewPilot.Agents.Execution;

/// <summary>
/// 运行中的 AgentRun 取消登记表。
/// API 的"取消（接管）"通过这里向正在执行的模型调用发出取消信号；
/// 排队中（尚未注册）的运行由执行器在出队时检查数据库状态后跳过。
/// </summary>
public sealed class AgentRunCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    /// <summary>为一次正在执行的 Run 注册取消源（与宿主停止令牌联动）。</summary>
    public CancellationTokenSource Register(Guid runId, CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _sources[runId] = cts;
        return cts;
    }

    /// <summary>解除注册并释放取消源。</summary>
    public void Unregister(Guid runId, CancellationTokenSource cts)
    {
        _sources.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(runId, cts));
        cts.Dispose();
    }

    /// <summary>请求取消；仅当该 Run 正在执行时返回 true。</summary>
    public bool Cancel(Guid runId)
    {
        if (!_sources.TryGetValue(runId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
