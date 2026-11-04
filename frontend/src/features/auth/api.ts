import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { AuthStatus } from "@/lib/types";

export const authKeys = {
  status: ["auth", "status"] as const,
};

export function useAuthStatus() {
  return useQuery({
    queryKey: authKeys.status,
    queryFn: () => api.get<AuthStatus>("/auth/status"),
    retry: false,
    staleTime: 30_000,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (password: string) => api.post<AuthStatus>("/auth/login", { password }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: authKeys.status }),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post("/auth/logout"),
    onSuccess: () => {
      void queryClient.invalidateQueries();
    },
  });
}
