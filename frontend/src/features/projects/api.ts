import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  Activity,
  Attachment,
  BatchWorkItemsResult,
  Burndown,
  Comment,
  EstimateDeviationReport,
  HoursReport,
  HumanCostReport,
  IterationDetail,
  IterationListItem,
  Milestone,
  ProjectDetail,
  ProjectMember,
  ProjectSummary,
  WorkItemDetail,
  WorkItemListItem,
  WorkLog,
} from "@/lib/types";

export const projectKeys = {
  all: ["projects"] as const,
  iterations: (projectId: string) => ["projects", projectId, "iterations"] as const,
  workItems: (projectId: string, type?: string, tag?: string) =>
    ["projects", projectId, "workitems", type ?? "all", tag ?? "all"] as const,
  workItem: (workItemId: string) => ["workitems", workItemId] as const,
  workLogs: (workItemId: string) => ["workitems", workItemId, "worklogs"] as const,
  comments: (workItemId: string) => ["workitems", workItemId, "comments"] as const,
  attachments: (workItemId: string) => ["workitems", workItemId, "attachments"] as const,
  milestones: (projectId: string) => ["projects", projectId, "milestones"] as const,
  prototypes: (projectId: string) => ["projects", projectId, "prototypes"] as const,
  activities: (projectId: string) => ["projects", projectId, "activities"] as const,
  burndown: (iterationId: string) => ["iterations", iterationId, "burndown"] as const,
  hoursReport: (projectId: string, filters?: ReportFilters) =>
    [
      "projects",
      projectId,
      "hours-report",
      filters?.iterationId ?? "all",
      filters?.from ?? "",
      filters?.to ?? "",
    ] as const,
  humanCost: (projectId: string) => ["projects", projectId, "human-cost-report"] as const,
  estimateDeviation: (projectId: string) => ["projects", projectId, "estimate-deviation"] as const,
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

export function useSetDefaultTeam(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (teamId: string | null) =>
      api.put<ProjectDetail>(`/projects/${projectId}/default-team`, { teamId }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
      void queryClient.invalidateQueries({ queryKey: ["agent-teams", projectId] });
    },
  });
}

export function useSetTypeTeams(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      requirementTeamId: string | null;
      defectTeamId: string | null;
      taskTeamId: string | null;
    }) => api.put<ProjectDetail>(`/projects/${projectId}/type-teams`, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
      void queryClient.invalidateQueries({ queryKey: ["agent-teams", projectId] });
    },
  });
}

export function useWorkItems(
  projectId: string | undefined,
  type?: string,
  tag?: string,
  relatedRequirementId?: string,
) {
  const search = new URLSearchParams();
  if (type) {
    search.set("type", type);
  }
  if (tag) {
    search.set("tag", tag);
  }
  if (relatedRequirementId) {
    search.set("relatedRequirementId", relatedRequirementId);
  }
  const suffix = search.size > 0 ? `?${search.toString()}` : "";

  return useQuery({
    queryKey: [...projectKeys.workItems(projectId ?? "", type, tag), relatedRequirementId ?? ""],
    queryFn: () => api.get<WorkItemListItem[]>(`/projects/${projectId}/workitems${suffix}`),
    enabled: projectId !== undefined,
  });
}

export interface BatchWorkItemsBody {
  ids: string[];
  action: "status" | "assign" | "delete";
  toStatus?: string;
  assigneeName?: string;
}

export function useBatchWorkItems(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: BatchWorkItemsBody) =>
      api.post<BatchWorkItemsResult>("/workitems/batch", body),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "workitems"],
      });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
    },
  });
}

export function useWorkLogs(workItemId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.workLogs(workItemId ?? ""),
    queryFn: () => api.get<WorkLog[]>(`/workitems/${workItemId}/worklogs`),
    enabled: workItemId !== undefined,
  });
}

export function useWorkItem(workItemId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.workItem(workItemId ?? ""),
    queryFn: () => api.get<WorkItemDetail>(`/workitems/${workItemId}`),
    enabled: workItemId !== undefined,
  });
}

export function useComments(workItemId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.comments(workItemId ?? ""),
    queryFn: () => api.get<Comment[]>(`/workitems/${workItemId}/comments`),
    enabled: workItemId !== undefined,
  });
}

export function useAddComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { workItemId: string; content: string; mentionAgentId?: string }) =>
      api.post<{ comment: Comment; agentRunId: string | null }>(
        `/workitems/${variables.workItemId}/comments`,
        { content: variables.content, mentionAgentId: variables.mentionAgentId },
      ),
    onSuccess: (_result, variables) => {
      void queryClient.invalidateQueries({
        queryKey: projectKeys.comments(variables.workItemId),
      });
      void queryClient.invalidateQueries({ queryKey: ["agent-runs"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useDeleteComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/comments/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useAttachments(workItemId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.attachments(workItemId ?? ""),
    queryFn: () => api.get<Attachment[]>(`/workitems/${workItemId}/attachments`),
    enabled: workItemId !== undefined,
  });
}

export function useUploadAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { workItemId: string; file: File }) => {
      const form = new FormData();
      form.append("file", variables.file);
      return api.postForm<Attachment>(`/workitems/${variables.workItemId}/attachments`, form);
    },
    onSuccess: (_result, variables) => {
      void queryClient.invalidateQueries({
        queryKey: projectKeys.attachments(variables.workItemId),
      });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useDeleteAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/attachments/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useMilestones(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.milestones(projectId ?? ""),
    queryFn: () => api.get<Milestone[]>(`/projects/${projectId}/milestones`),
    enabled: projectId !== undefined,
  });
}

export function useCreateMilestone(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; description?: string; date: string }) =>
      api.post<Milestone>(`/projects/${projectId}/milestones`, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectKeys.milestones(projectId) });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useUpdateMilestone(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { id: string; name: string; description?: string; date: string }) =>
      api.put<Milestone>(`/milestones/${variables.id}`, {
        name: variables.name,
        description: variables.description,
        date: variables.date,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectKeys.milestones(projectId) });
    },
  });
}

export function useDeleteMilestone(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/milestones/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectKeys.milestones(projectId) });
      void queryClient.invalidateQueries({ queryKey: projectKeys.iterations(projectId) });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
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

export function useActivities(
  projectId: string | undefined,
  workItemId?: string,
  eventType?: string,
) {
  const search = new URLSearchParams({ take: "30" });
  if (workItemId) {
    search.set("workItemId", workItemId);
  }
  if (eventType) {
    search.set("eventType", eventType);
  }

  return useQuery({
    queryKey: [...projectKeys.activities(projectId ?? ""), workItemId ?? "", eventType ?? ""],
    queryFn: () => api.get<Activity[]>(`/projects/${projectId}/activities?${search.toString()}`),
    enabled: projectId !== undefined,
  });
}

export interface ReportFilters {
  iterationId?: string;
  from?: string;
  to?: string;
}

function reportQuery(filters?: ReportFilters): string {
  const search = new URLSearchParams();
  if (filters?.iterationId) {
    search.set("iterationId", filters.iterationId);
  }
  if (filters?.from) {
    search.set("from", filters.from);
  }
  if (filters?.to) {
    search.set("to", filters.to);
  }
  const suffix = search.toString();
  return suffix === "" ? "" : `?${suffix}`;
}

export function useHoursReport(projectId: string | undefined, filters?: ReportFilters) {
  return useQuery({
    queryKey: projectKeys.hoursReport(projectId ?? "", filters),
    queryFn: () =>
      api.get<HoursReport>(`/projects/${projectId}/hours-report${reportQuery(filters)}`),
    enabled: projectId !== undefined,
  });
}

export function useHumanCostReport(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.humanCost(projectId ?? ""),
    queryFn: () => api.get<HumanCostReport>(`/projects/${projectId}/human-cost-report`),
    enabled: projectId !== undefined,
  });
}

export function useEstimateDeviation(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.estimateDeviation(projectId ?? ""),
    queryFn: () => api.get<EstimateDeviationReport>(`/projects/${projectId}/estimate-deviation`),
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

export function useUpdateIteration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: {
      id: string;
      body: {
        name: string;
        goal?: string;
        startDate: string;
        endDate: string;
        milestoneId?: string;
      };
    }) => api.put<IterationDetail>(`/iterations/${variables.id}`, variables.body),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: projectKeys.iterations(projectId) }),
  });
}

export function useCreateIteration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      name: string;
      goal?: string;
      startDate: string;
      endDate: string;
      milestoneId?: string;
    }) => api.post<IterationDetail>(`/projects/${projectId}/iterations`, body),
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

export function useUpdateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: {
      id: string;
      name: string;
      description?: string;
      repoUrl?: string;
      localPath?: string;
    }) =>
      api.put<ProjectDetail>(`/projects/${variables.id}`, {
        name: variables.name,
        description: variables.description,
        repoUrl: variables.repoUrl,
        localPath: variables.localPath,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  });
}

export function useArchiveProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { id: string; archive: boolean }) =>
      api.post<ProjectDetail>(
        `/projects/${variables.id}/${variables.archive ? "archive" : "unarchive"}`,
      ),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  });
}

export function useProjectMembers(projectId: string | undefined) {
  return useQuery({
    queryKey: ["projects", projectId ?? "", "members"] as const,
    queryFn: () => api.get<ProjectMember[]>(`/projects/${projectId}/members`),
    enabled: projectId !== undefined,
  });
}

export function useAddProjectMember(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { displayName?: string; role?: string; agentId?: string }) =>
      api.post<ProjectMember>(`/projects/${projectId}/members`, body),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "members"] as const,
      }),
  });
}

export function useUpdateProjectMember(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: {
      id: string;
      displayName: string;
      role: string;
      hourlyRate?: number;
    }) =>
      api.put<ProjectMember>(`/members/${variables.id}`, {
        displayName: variables.displayName,
        role: variables.role,
        hourlyRate: variables.hourlyRate,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "members"] as const,
      });
      void queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "human-cost-report"] as const,
      });
    },
  });
}

export function useDeleteProjectMember(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/members/${id}`),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "members"] as const,
      }),
  });
}

export interface UpdateWorkItemBody {
  title: string;
  description?: string;
  acceptanceCriteria?: string;
  priority?: string;
  storyPoints?: number;
  estimatedHours?: number;
  remainingHours?: number;
  dueDate?: string;
  iterationId?: string;
  assigneeName?: string;
  severity?: string;
  reproSteps?: string;
  environment?: string;
  tags?: string;
  parentWorkItemId?: string;
  branchUrl?: string;
  prUrl?: string;
  relatedRequirementId?: string;
}

export function useUpdateWorkItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { id: string; body: UpdateWorkItemBody }) =>
      api.put<WorkItemDetail>(`/workitems/${variables.id}`, variables.body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useDeleteWorkItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/workitems/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["workitems"] });
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
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
  tags?: string;
  iterationId?: string;
  dueDate?: string;
  parentWorkItemId?: string;
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
