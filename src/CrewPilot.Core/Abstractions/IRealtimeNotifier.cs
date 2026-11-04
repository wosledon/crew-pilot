namespace CrewPilot.Core.Abstractions;

/// <summary>
/// 实时事件通知抽象（SignalR 由 Api 层实现；Core/Agents 只发布领域事件）。
/// 单用户产品广播给所有已连接客户端，前端按 projectId 决定是否刷新。
/// </summary>
public interface IRealtimeNotifier
{
    Task WorkItemChangedAsync(
        Guid projectId, Guid workItemId, string status, CancellationToken ct = default);

    Task AgentRunChangedAsync(
        Guid projectId, Guid agentRunId, string status, CancellationToken ct = default);

    Task BlockerChangedAsync(
        Guid projectId, Guid blockerId, string status, CancellationToken ct = default);
}
