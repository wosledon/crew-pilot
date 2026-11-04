import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  AgentDetail,
  AgentListItem,
  AgentRunDetail,
  AgentRunListItem,
  ProviderDetail,
  ProviderListItem,
} from "@/lib/types";

export const agentKeys = {
  providers: ["providers"] as const,
  providerDetail: (id: string) => ["providers", id] as const,
  agents: ["agents"] as const,
  agentDetail: (id: string) => ["agents", id] as const,
  runs: (params: { projectId?: string; workItemId?: string; agentId?: string }) =>
    [
      "agent-runs",
      params.projectId ?? "all",
      params.workItemId ?? "all",
      params.agentId ?? "all",
    ] as const,
  runDetail: (id: string) => ["agent-runs", id] as const,
};

export function useProviders() {
  return useQuery({
    queryKey: agentKeys.providers,
    queryFn: () => api.get<ProviderListItem[]>("/providers"),
  });
}

export function useProvider(providerId: string | undefined) {
  return useQuery({
    queryKey: agentKeys.providerDetail(providerId ?? ""),
    queryFn: () => api.get<ProviderDetail>(`/providers/${providerId}`),
    enabled: providerId !== undefined,
  });
}

export function useCreateProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; baseUrl: string; apiKey: string }) =>
      api.post<ProviderDetail>("/providers", body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: agentKeys.providers }),
  });
}

export function useDeleteProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/providers/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: agentKeys.providers }),
  });
}

export function useTestProvider() {
  return useMutation({
    mutationFn: (id: string) =>
      api.post<{ ok: boolean; message: string; modelCount: number }>(`/providers/${id}/test`),
  });
}

export function useSyncProviderModels() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { id: string; catalogKey?: string }) =>
      api.post<{ synced: number }>(
        `/providers/${variables.id}/sync-models${variables.catalogKey ? `?catalogKey=${variables.catalogKey}` : ""}`,
      ),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["providers"] }),
  });
}

export function useAgents() {
  return useQuery({
    queryKey: agentKeys.agents,
    queryFn: () => api.get<AgentListItem[]>("/agents"),
  });
}

export function useAgent(agentId: string | undefined) {
  return useQuery({
    queryKey: agentKeys.agentDetail(agentId ?? ""),
    queryFn: () => api.get<AgentDetail>(`/agents/${agentId}`),
    enabled: agentId !== undefined,
  });
}

export function useCreateAgent() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      name: string;
      job: string;
      providerId: string;
      model: string;
      systemPrompt: string;
      tools?: string;
      commandWhitelist?: string;
    }) => api.post<AgentDetail>("/agents", body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: agentKeys.agents }),
  });
}

export function useDeleteAgent() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/agents/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: agentKeys.agents }),
  });
}

export function useAgentRuns(params: {
  projectId?: string;
  workItemId?: string;
  agentId?: string;
}) {
  const search = new URLSearchParams();
  if (params.projectId) {
    search.set("projectId", params.projectId);
  }
  if (params.workItemId) {
    search.set("workItemId", params.workItemId);
  }
  if (params.agentId) {
    search.set("agentId", params.agentId);
  }
  const suffix = search.size > 0 ? `?${search.toString()}` : "";

  return useQuery({
    queryKey: agentKeys.runs(params),
    queryFn: () => api.get<AgentRunListItem[]>(`/agent-runs${suffix}`),
    refetchInterval: (query) => {
      const data = query.state.data;
      const running = data?.some((run) => ["queued", "running"].includes(run.status));
      return running ? 2000 : false;
    },
  });
}

export function useAgentRun(runId: string | undefined) {
  return useQuery({
    queryKey: agentKeys.runDetail(runId ?? ""),
    queryFn: () => api.get<AgentRunDetail>(`/agent-runs/${runId}`),
    enabled: runId !== undefined,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === "queued" || status === "running" ? 2000 : false;
    },
  });
}

export function useAssignAgent() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { workItemId: string; agentId: string }) =>
      api.post<AgentRunListItem>(`/workitems/${variables.workItemId}/assign-agent`, {
        agentId: variables.agentId,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["agent-runs"] }),
  });
}

export function useCancelAgentRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post(`/agent-runs/${id}/cancel`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["agent-runs"] }),
  });
}
