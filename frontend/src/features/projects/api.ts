import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  Activity,
  Burndown,
  HoursReport,
  IterationDetail,
  IterationListItem,
  ProjectDetail,
  ProjectSummary,
  WorkItemDetail,
  WorkItemListItem,
  WorkLog,
} from "@/lib/types";

export const projectKeys = {
  all: ["projects"] as const,
  iterations: (projectId: string) => ["projects", projectId, "iterations"] as const,
  workItems: (projectId: string, type?: string) =>
    ["projects", projectId, "workitems", type ?? "all"] as const,
  workLogs: (workItemId: string) => ["workitems", workItemId, "worklogs"] as const,
  prototypes: (projectId: string) => ["projects", projectId, "prototypes"] as const,
  activities: (projectId: string) => ["projects", projectId, "activities"] as const,
  burndown: (iterationId: string) => ["iterations", iterationId, "burndown"] as const,
  hoursReport: (projectId: string) => ["projects", projectId, "hours-report"] as const,
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
      api.get<WorkItemListItem[]>(`/projects/${projectId}/workitems${type ? `?type=${type}` : ""}`),
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

export function useIterations(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.iterations(projectId ?? ""),
    queryFn: () => api.get<IterationListItem[]>(`/projects/${projectId}/iterations`),
    enabled: projectId !== undefined,
  });
}

export function useBurndown(iterationId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.burndown(iterationId ?? ""),
    queryFn: () => api.get<Burndown>(`/iterations/${iterationId}/burndown`),
    enabled: iterationId !== undefined,
  });
}

export function useActivities(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.activities(projectId ?? ""),
    queryFn: () => api.get<Activity[]>(`/projects/${projectId}/activities?take=30`),
    enabled: projectId !== undefined,
  });
}

export function useHoursReport(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.hoursReport(projectId ?? ""),
    queryFn: () => api.get<HoursReport>(`/projects/${projectId}/hours-report`),
    enabled: projectId !== undefined,
  });
}

export function useStartIteration() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<IterationDetail>(`/iterations/${id}/start`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["projects"] }),
  });
}

export function useCompleteIteration() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<IterationDetail>(`/iterations/${id}/complete`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["projects"] }),
  });
}

export function useCreateIteration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; goal?: string; startDate: string; endDate: string }) =>
      api.post<IterationDetail>(`/projects/${projectId}/iterations`, body),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: projectKeys.iterations(projectId) }),
  });
}

export function useDeleteIteration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/iterations/${id}`),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: projectKeys.iterations(projectId) }),
  });
}

export interface CreateProjectBody {
  key: string;
  name: string;
  description?: string;
  repoUrl?: string;
  localPath?: string;
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateProjectBody) => api.post<ProjectDetail>("/projects", body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/projects/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: projectKeys.all }),
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
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["projects"] }),
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
