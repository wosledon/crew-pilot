using System.Threading.Channels;

namespace CrewPilot.Agents.Execution;

/// <summary>AgentRun 执行队列：API 入队后立即返回，由后台 Worker 异步执行。</summary>
public sealed class AgentRunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid runId) => _channel.Writer.WriteAsync(runId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
