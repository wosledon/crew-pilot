import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Prototype } from "@/lib/types";

export const prototypeKeys = {
  list: (projectId: string) => ["projects", projectId, "prototypes"] as const,
};

export function usePrototypes(projectId: string | undefined) {
  return useQuery({
    queryKey: prototypeKeys.list(projectId ?? ""),
    queryFn: () => api.get<Prototype[]>(`/projects/${projectId}/prototypes`),
    enabled: projectId !== undefined,
  });
}

export interface CreatePrototypeBody {
  name: string;
  type: string;
  content: string;
}

export function useCreatePrototype(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreatePrototypeBody) =>
      api.post<Prototype>(`/projects/${projectId}/prototypes`, body),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: prototypeKeys.list(projectId) }),
  });
}

export interface UpdatePrototypeBody {
  id: string;
  name: string;
  content: string;
}

export function useUpdatePrototype(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdatePrototypeBody) =>
      api.put<Prototype>(`/prototypes/${body.id}`, {
        name: body.name,
        content: body.content,
      }),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: prototypeKeys.list(projectId) }),
  });
}

export function useDeletePrototype(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/prototypes/${id}`),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: prototypeKeys.list(projectId) }),
  });
}
