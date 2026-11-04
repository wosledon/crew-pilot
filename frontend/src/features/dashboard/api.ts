import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Dashboard } from "@/lib/types";

export const dashboardKeys = {
  all: ["dashboard"] as const,
};

/** 驾驶舱聚合数据：15s 轮询 + SignalR 事件刷新（见 lib/realtime.ts） */
export function useDashboard() {
  return useQuery({
    queryKey: dashboardKeys.all,
    queryFn: () => api.get<Dashboard>("/dashboard"),
    refetchInterval: 15_000,
  });
}
