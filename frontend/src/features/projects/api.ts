import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  ProjectDetail,
  ProjectSummary,
  WorkItemDetail,
  WorkItemListItem,
  WorkLog,
} from "@/lib/types";

export const projectKeys = {
  all: ["projects"] as const,
  workItems: (projectId: string, type?: string) =>
    ["projects", projectId, "workitems", type ?? "all"] as const,
  workLogs: (workItemId: string) => ["workitems", workItemId, "worklogs"] as const,
  prototypes: (projectId: string) =>
    ["projects", projectId, "prototypes"] as const,
};

export function useProjects() {
  return useQuery({
    queryKey: projectKeys.all,
    queryFn: () => api.get<ProjectSummary[]>("/projects"),
  });
}

export function useProject(projectId: string | undefined) {
  return useQuery({
    queryKey: ["projects", projectId ?? ""] as const,
    queryFn: () => api.get<ProjectDetail>(`/projects/${projectId}`),
    enabled: projectId !== undefined,
  });
}

export function useWorkItems(projectId: string | undefined, type?: string) {
  return useQuery({
    queryKey: projectKeys.workItems(projectId ?? "", type),
    queryFn: () =>
      api.get<WorkItemListItem[]>(
        `/projects/${projectId}/workitems${type ? `?type=${type}` : ""}`,
      ),
    enabled: projectId !== undefined,
  });
}

export function useWorkLogs(workItemId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.workLogs(workItemId ?? ""),
    queryFn: () => api.get<WorkLog[]>(`/workitems/${workItemId}/worklogs`),
    enabled: workItemId !== undefined,
  });
}

export interface CreateProjectBody {
  key: string;
  name: string;
  description?: string;
  repoUrl?: string;
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateProjectBody) =>
      api.post<ProjectDetail>("/projects", body),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/projects/${id}`),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  });
}

export interface CreateWorkItemBody {
  type: string;
  title: string;
  description?: string;
  acceptanceCriteria?: string;
  priority?: string;
  estimatedHours?: number;
  assigneeName?: string;
  severity?: string;
  reproSteps?: string;
  environment?: string;
}

export function useCreateWorkItem(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateWorkItemBody) =>
      api.post<WorkItemDetail>(`/projects/${projectId}/workitems`, body),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "workitems"],
      }),
  });
}

export function useTransitionWorkItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { id: string; toStatus: string }) =>
      api.post<WorkItemDetail>(`/workitems/${variables.id}/status`, {
        toStatus: variables.toStatus,
      }),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: ["projects"] }),
  });
}

export function useAddWorkLog() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { workItemId: string; hours: number; note?: string }) =>
      api.post<WorkLog>(`/workitems/${variables.workItemId}/worklogs`, {
        hours: variables.hours,
        note: variables.note,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}
