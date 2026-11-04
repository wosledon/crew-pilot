import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { ProjectIntegration, PullRequestStatus, ReleaseResult } from "@/lib/types";

export const integrationKeys = {
  integration: (projectId: string) => ["projects", projectId, "integration"] as const,
};

export function useProjectIntegration(projectId: string | undefined) {
  return useQuery({
    queryKey: integrationKeys.integration(projectId ?? ""),
    queryFn: () => api.get<ProjectIntegration>(`/projects/${projectId}/integration`),
    enabled: projectId !== undefined,
    retry: false,
  });
}

export function useUpsertIntegration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { platform: string; token?: string; repository?: string }) =>
      api.put<ProjectIntegration>(`/projects/${projectId}/integration`, body),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: integrationKeys.integration(projectId),
      }),
  });
}

export function useDeleteIntegration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.delete(`/projects/${projectId}/integration`),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: integrationKeys.integration(projectId),
      }),
  });
}

export function useTestIntegration(projectId: string) {
  return useMutation({
    mutationFn: () =>
      api.post<{ ok: boolean; message: string }>(`/projects/${projectId}/integration/test`),
  });
}

export function useCreatePullRequest(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { branch: string; title: string; body?: string }) =>
      api.post<{ url: string }>(`/workitems/${workItemId}/pull-request`, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useSyncPullRequest(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<PullRequestStatus>(`/workitems/${workItemId}/pull-request/sync`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useMergePullRequest(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<PullRequestStatus>(`/workitems/${workItemId}/pull-request/merge`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useCreateRelease(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { tag: string; title?: string; notes?: string }) =>
      api.post<ReleaseResult>(`/projects/${projectId}/releases`, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
      void queryClient.invalidateQueries({ queryKey: ["projects", projectId, "activities"] });
    },
  });
}
