import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Prototype, PrototypeFile, PrototypeImage, PrototypeVersion } from "@/lib/types";

export const prototypeKeys = {
  list: (projectId: string) => ["projects", projectId, "prototypes"] as const,
  versions: (prototypeId: string) => ["prototypes", prototypeId, "versions"] as const,
  files: (prototypeId: string) => ["prototypes", prototypeId, "files"] as const,
};

export function usePrototypeFiles(prototypeId: string | undefined) {
  return useQuery({
    queryKey: prototypeKeys.files(prototypeId ?? ""),
    queryFn: () => api.get<PrototypeFile[]>(`/prototypes/${prototypeId}/files`),
    enabled: prototypeId !== undefined,
  });
}

export function useUpsertPrototypeFile(prototypeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { path: string; content: string }) =>
      api.post<PrototypeFile>(`/prototypes/${prototypeId}/files`, body),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: prototypeKeys.files(prototypeId),
      }),
  });
}

export function useDeletePrototypeFile(prototypeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (fileId: string) => api.delete(`/prototype-files/${fileId}`),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: prototypeKeys.files(prototypeId),
      }),
  });
}

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
  requirementId?: string;
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
  requirementId?: string;
}

export function useUpdatePrototype(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdatePrototypeBody) =>
      api.put<Prototype>(`/prototypes/${body.id}`, {
        name: body.name,
        content: body.content,
        requirementId: body.requirementId,
      }),
    onSuccess: (prototype) => {
      void queryClient.invalidateQueries({ queryKey: prototypeKeys.list(projectId) });
      void queryClient.invalidateQueries({
        queryKey: prototypeKeys.versions(prototype.id),
      });
    },
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

export function usePrototypeVersions(prototypeId: string | null) {
  return useQuery({
    queryKey: prototypeKeys.versions(prototypeId ?? ""),
    queryFn: () => api.get<PrototypeVersion[]>(`/prototypes/${prototypeId}/versions`),
    enabled: prototypeId !== null,
  });
}

export function useRestorePrototypeVersion(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: { id: string; version: number }) =>
      api.post<Prototype>(`/prototypes/${variables.id}/versions/${variables.version}/restore`),
    onSuccess: (prototype) => {
      void queryClient.invalidateQueries({ queryKey: prototypeKeys.list(projectId) });
      void queryClient.invalidateQueries({
        queryKey: prototypeKeys.versions(prototype.id),
      });
    },
  });
}

export function useUploadPrototypeImage(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return api.postForm<PrototypeImage>(`/projects/${projectId}/prototype-images`, form);
    },
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: prototypeKeys.list(projectId) }),
  });
}

/** 图片存储路径 → 可访问 URL（兼容 Windows 反斜杠路径）。 */
export function prototypeImageUrl(path: string): string {
  return `/api/prototype-images/${path.replaceAll("\\", "/")}`;
}
