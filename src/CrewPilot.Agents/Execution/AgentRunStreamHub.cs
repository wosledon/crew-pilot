using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CrewPilot.Agents.Execution;

/// <summary>
/// AgentRun 输出流广播中心：执行器发布增量 Delta，SSE 订阅者消费。
/// 仅在内存中广播，不持久化（持久化仍由 RunLog 负责）。
/// </summary>
public sealed class AgentRunStreamHub
{
    private sealed class Subscription
    {
        public required Channel<string> Channel { get; init; }
    }

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Subscription>> _subscriptions = new();

    public (Guid SubscriptionId, ChannelReader<string> Reader) Subscribe(Guid runId)
    {
        var channel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var id = Guid.NewGuid();
        var set = _subscriptions.GetOrAdd(runId, _ => new ConcurrentDictionary<Guid, Subscription>());
        set[id] = new Subscription { Channel = channel };
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid runId, Guid subscriptionId)
    {
        if (_subscriptions.TryGetValue(runId, out var set) && set.TryRemove(subscriptionId, out var sub))
        {
            sub.Channel.Writer.TryComplete();
        }
    }

    public void Publish(Guid runId, string delta)
    {
        if (delta.Length == 0 || !_subscriptions.TryGetValue(runId, out var set))
        {
            return;
        }

        foreach (var subscription in set.Values)
        {
            subscription.Channel.Writer.TryWrite(delta);
        }
    }

    public void Complete(Guid runId)
    {
        if (_subscriptions.TryRemove(runId, out var set))
        {
            foreach (var subscription in set.Values)
            {
                subscription.Channel.Writer.TryComplete();
            }
        }
    }
}
