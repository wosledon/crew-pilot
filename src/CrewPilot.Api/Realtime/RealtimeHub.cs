using CrewPilot.Core.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace CrewPilot.Api.Realtime;

/// <summary>实时推送 Hub：客户端只接收事件，不发送业务命令。</summary>
public sealed class RealtimeHub : Hub;

/// <summary>SignalR 广播实现：单用户产品向所有连接广播，负载携带 projectId 供前端过滤。</summary>
public sealed class SignalRRealtimeNotifier(IHubContext<RealtimeHub> hub) : IRealtimeNotifier
{
    public Task WorkItemChangedAsync(
        Guid projectId, Guid workItemId, string status, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync(
            RealtimeEventNames.WorkItemChanged,
            new { projectId, workItemId, status },
            ct);

    public Task AgentRunChangedAsync(
        Guid projectId, Guid agentRunId, string status, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync(
            RealtimeEventNames.AgentRunChanged,
            new { projectId, agentRunId, status },
            ct);

    public Task BlockerChangedAsync(
        Guid projectId, Guid blockerId, string status, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync(
            RealtimeEventNames.BlockerChanged,
            new { projectId, blockerId, status },
            ct);
}

public static class RealtimeEventNames
{
    public const string WorkItemChanged = "WorkItemChanged";

    public const string AgentRunChanged = "AgentRunChanged";

    public const string BlockerChanged = "BlockerChanged";
}
