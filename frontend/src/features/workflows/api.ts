import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  Blocker,
  BlockerResolution,
  Workflow,
  WorkflowRun,
  WorkflowRunDetail,
  WorkflowTemplate,
} from "@/lib/types";

export const workflowKeys = {
  workflows: (projectId: string) => ["projects", projectId, "workflows"] as const,
  templates: ["workflow-templates"] as const,
  runs: (params: { projectId?: string; workItemId?: string }) =>
    ["workflow-runs", params.projectId ?? "all", params.workItemId ?? "all"] as const,
  run: (id: string) => ["workflow-runs", id] as const,
  blockers: (projectId: string, status?: string) =>
    ["projects", projectId, "blockers", status ?? "open"] as const,
};

export function useWorkflows(projectId: string | undefined) {
  return useQuery({
    queryKey: workflowKeys.workflows(projectId ?? ""),
    queryFn: () => api.get<Workflow[]>(`/projects/${projectId}/workflows`),
    enabled: projectId !== undefined,
  });
}

export function useWorkflowTemplates() {
  return useQuery({
    queryKey: workflowKeys.templates,
    queryFn: () => api.get<WorkflowTemplate[]>("/workflows/templates"),
  });
}

export function useCreateWorkflowFromTemplate(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { templateKey: string; name?: string }) =>
      api.post<Workflow>(`/projects/${projectId}/workflows/from-template`, body),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: workflowKeys.workflows(projectId),
      }),
  });
}

export function useDeleteWorkflow(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/workflows/${id}`),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: workflowKeys.workflows(projectId),
      }),
  });
}

export function useStartWorkflowRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { workflowId: string; workItemId: string }) =>
      api.post<WorkflowRun>(`/workflows/${variables.workflowId}/runs`, {
        workItemId: variables.workItemId,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workflow-runs"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
    },
  });
}

export function useWorkflowRuns(params: { projectId?: string; workItemId?: string }) {
  const query = new URLSearchParams();
  if (params.projectId !== undefined) {
    query.set("projectId", params.projectId);
  }
  if (params.workItemId !== undefined) {
    query.set("workItemId", params.workItemId);
  }

  return useQuery({
    queryKey: workflowKeys.runs(params),
    queryFn: () => api.get<WorkflowRun[]>(`/workflow-runs?${query.toString()}`),
    enabled: params.projectId !== undefined || params.workItemId !== undefined,
    refetchInterval: 5000,
  });
}

export function useWorkflowRun(id: string | undefined) {
  return useQuery({
    queryKey: workflowKeys.run(id ?? ""),
    queryFn: () => api.get<WorkflowRunDetail>(`/workflow-runs/${id}`),
    enabled: id !== undefined,
    refetchInterval: 3000,
  });
}

export function useCancelWorkflowRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post(`/workflow-runs/${id}/cancel`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workflow-runs"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useBlockers(projectId: string | undefined, status = "open") {
  return useQuery({
    queryKey: workflowKeys.blockers(projectId ?? "", status),
    queryFn: () => api.get<Blocker[]>(`/projects/${projectId}/blockers?status=${status}`),
    enabled: projectId !== undefined,
    refetchInterval: 5000,
  });
}

export function useResolveBlocker(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { id: string; resolution: BlockerResolution; note?: string }) =>
      api.post<Blocker>(`/blockers/${variables.id}/resolve`, {
        resolution: variables.resolution,
        note: variables.note,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "blockers"],
      });
      void queryClient.invalidateQueries({ queryKey: ["workflow-runs"] });
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}
