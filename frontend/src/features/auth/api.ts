import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { typedApi, unwrapResponse } from "@/lib/typed-api";
import type { AuthStatus } from "@/lib/types";

export const authKeys = {
  status: ["auth", "status"] as const,
};

export function useAuthStatus() {
  return useQuery({
    queryKey: authKeys.status,
    queryFn: async () => unwrapResponse<AuthStatus>(await typedApi.GET("/api/auth/status")),
    retry: false,
    staleTime: 30_000,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (password: string) =>
      unwrapResponse<AuthStatus>(await typedApi.POST("/api/auth/login", { body: { password } })),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: authKeys.status }),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await typedApi.POST("/api/auth/logout");
    },
    onSuccess: () => {
      void queryClient.invalidateQueries();
    },
  });
}
