import { useEffect } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";

const reconnectedEvents = {
  workItem: "WorkItemChanged",
  agentRun: "AgentRunChanged",
  blocker: "BlockerChanged",
} as const;

/**
 * 订阅后端 SignalR 实时事件，事件到达时按查询根键刷新对应缓存。
 * 断线自动重连；后端未启动时静默失败，页面仍可用轮询兜底。
 */
export function useRealtimeUpdates() {
  const queryClient = useQueryClient();

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/realtime")
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    const invalidate = (roots: string[]) => {
      void queryClient.invalidateQueries({
        predicate: (query) =>
          Array.isArray(query.queryKey) && roots.includes(String(query.queryKey[0])),
      });
    };

    connection.on(reconnectedEvents.workItem, () =>
      invalidate(["workitems", "projects", "agent-runs", "dashboard"]),
    );
    connection.on(reconnectedEvents.agentRun, () =>
      invalidate(["agent-runs", "workitems", "projects", "dashboard"]),
    );
    connection.on(reconnectedEvents.blocker, () =>
      invalidate(["blockers", "workflows", "projects", "dashboard"]),
    );

    let disposed = false;
    let retryTimer: number | undefined;

    const start = async () => {
      try {
        await connection.start();
      } catch {
        // 后端暂不可用时静默重试
        if (!disposed) {
          retryTimer = window.setTimeout(() => void start(), 5000);
        }
      }
    };

    void start();

    connection.onclose(() => {
      if (!disposed) {
        retryTimer = window.setTimeout(() => void start(), 5000);
      }
    });

    return () => {
      disposed = true;
      if (retryTimer !== undefined) {
        window.clearTimeout(retryTimer);
      }
      void connection.stop();
    };
  }, [queryClient]);
}
