import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  AgentDetail,
  AgentListItem,
  AgentRunDetail,
  AgentRunListItem,
  AgentTeam,
  ModelDto,
  ProviderDetail,
  ProviderListItem,
  WorkItemAgentActivity,
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

export function useSetDefaultProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.put<ProviderDetail>(`/providers/${id}/default`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["providers"] }),
  });
}

export function useCreateProviderModel(providerId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      modelId: string;
      displayName?: string;
      contextLength?: number;
      capabilities?: string;
      inputPricePerMillion?: number;
      outputPricePerMillion?: number;
    }) => api.post<ModelDto>(`/providers/${providerId}/models`, body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["providers"] }),
  });
}

export function useUpdateProviderModel() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: {
      id: string;
      modelId: string;
      displayName?: string;
      contextLength?: number;
      capabilities?: string;
      inputPricePerMillion?: number;
      outputPricePerMillion?: number;
      enabled: boolean;
    }) =>
      api.put<ModelDto>(`/providers/models/${variables.id}`, {
        modelId: variables.modelId,
        displayName: variables.displayName,
        contextLength: variables.contextLength,
        capabilities: variables.capabilities,
        inputPricePerMillion: variables.inputPricePerMillion,
        outputPricePerMillion: variables.outputPricePerMillion,
        enabled: variables.enabled,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["providers"] }),
  });
}

export function useDeleteProviderModel() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/providers/models/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["providers"] }),
  });
}

export function useAgents() {
  return useQuery({
    queryKey: agentKeys.agents,
    queryFn: () => api.get<AgentListItem[]>("/agents"),
  });
}

/** 项目看板：每个工作项最近一次 Agent 活动（5s 轮询 + SignalR 事件刷新）。 */ export function useAgentActivity(
  projectId: string | undefined,
) {
  return useQuery({
    queryKey: ["projects", projectId ?? "", "agent-activity"] as const,
    queryFn: () => api.get<WorkItemAgentActivity[]>(`/projects/${projectId}/agent-activity`),
    enabled: projectId !== undefined,
    refetchInterval: 5000,
  });
}

export function useAgentTeams(projectId: string | undefined) {
  return useQuery({
    queryKey: ["agent-teams", projectId ?? ""] as const,
    queryFn: () => api.get<AgentTeam[]>(`/projects/${projectId}/agent-teams`),
    enabled: projectId !== undefined,
  });
}

export function useCreateAgentTeam(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; goal?: string; agentIds: string[] }) =>
      api.post<AgentTeam>(`/projects/${projectId}/agent-teams`, body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["agent-teams", projectId] }),
  });
}

export function useDeleteAgentTeam(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/agent-teams/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["agent-teams", projectId] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
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
      skillIds?: string[];
      mcpServerIds?: string[];
    }) => api.post<AgentDetail>("/agents", body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: agentKeys.agents }),
  });
}

export function useUpdateAgent(agentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      name: string;
      job: string;
      systemPrompt: string;
      tools?: string | null;
      commandWhitelist?: string | null;
      enabled: boolean;
      skillIds?: string[];
      mcpServerIds?: string[];
    }) => api.put<AgentDetail>(`/agents/${agentId}`, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: agentKeys.agents });
      void queryClient.invalidateQueries({ queryKey: agentKeys.agentDetail(agentId) });
    },
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

export function useRetryAgentRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<AgentRunListItem>(`/agent-runs/${id}/retry`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["agent-runs"] });
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
    },
  });
}
