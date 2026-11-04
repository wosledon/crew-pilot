import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { AuditLogEntry, CostReport, McpServer, Skill } from "@/lib/types";

export const platformKeys = {
  skills: ["skills"] as const,
  skill: (id: string) => ["skills", id] as const,
  mcpServers: ["mcp-servers"] as const,
  audit: (projectId?: string) => ["audit", projectId ?? "all"] as const,
  cost: (projectId: string) => ["projects", projectId, "cost-report"] as const,
};

export function useSkills() {
  return useQuery({
    queryKey: platformKeys.skills,
    queryFn: () => api.get<Skill[]>("/skills"),
  });
}

export function useCreateSkill() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      name: string;
      description?: string;
      promptContent: string;
      tools?: string;
      scriptPath?: string;
    }) => api.post<Skill>("/skills", body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: platformKeys.skills }),
  });
}

export function useUpdateSkill() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: {
      id: string;
      name: string;
      description?: string;
      promptContent: string;
      tools?: string;
      scriptPath?: string;
      enabled: boolean;
    }) =>
      api.put<Skill>(`/skills/${variables.id}`, {
        name: variables.name,
        description: variables.description,
        promptContent: variables.promptContent,
        tools: variables.tools,
        scriptPath: variables.scriptPath,
        enabled: variables.enabled,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: platformKeys.skills }),
  });
}

export function useDeleteSkill() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/skills/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: platformKeys.skills }),
  });
}

export function useMcpServers() {
  return useQuery({
    queryKey: platformKeys.mcpServers,
    queryFn: () => api.get<McpServer[]>("/mcp-servers"),
  });
}

export function useCreateMcpServer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; endpoint: string; transport: string; authToken?: string }) =>
      api.post<McpServer>("/mcp-servers", body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: platformKeys.mcpServers }),
  });
}

export function useUpdateMcpServer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: {
      id: string;
      name: string;
      endpoint: string;
      transport: string;
      authToken?: string;
      enabled: boolean;
    }) =>
      api.put<McpServer>(`/mcp-servers/${variables.id}`, {
        name: variables.name,
        endpoint: variables.endpoint,
        transport: variables.transport,
        authToken: variables.authToken,
        enabled: variables.enabled,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: platformKeys.mcpServers }),
  });
}

export function useDeleteMcpServer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/mcp-servers/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: platformKeys.mcpServers }),
  });
}

export function useTestMcpServer() {
  return useMutation({
    mutationFn: (id: string) =>
      api.post<{ ok: boolean; message: string }>(`/mcp-servers/${id}/test`),
  });
}

export function useAuditLogs(projectId?: string) {
  return useQuery({
    queryKey: platformKeys.audit(projectId),
    queryFn: () =>
      api.get<AuditLogEntry[]>(
        `/audit?take=200${projectId === undefined ? "" : `&projectId=${projectId}`}`,
      ),
  });
}

export function useCostReport(projectId: string | undefined) {
  return useQuery({
    queryKey: platformKeys.cost(projectId ?? ""),
    queryFn: () => api.get<CostReport>(`/projects/${projectId}/cost-report`),
    enabled: projectId !== undefined,
  });
}
