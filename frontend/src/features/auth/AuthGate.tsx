import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { LoadingState } from "@/components/ui/States";
import { useAuthStatus, authKeys } from "./api";
import { LoginPage } from "./LoginPage";

export interface AuthGateProps {
  children: React.ReactNode;
}

/** 认证门禁：启用认证且未登录时显示登录页；否则直接渲染应用。 */
export function AuthGate({ children }: AuthGateProps) {
  const status = useAuthStatus();
  const queryClient = useQueryClient();

  // API 返回 401 时重新检查认证状态（会话过期 → 回到登录页）
  useEffect(() => {
    const handler = () => void queryClient.invalidateQueries({ queryKey: authKeys.status });
    window.addEventListener("crewpilot:unauthorized", handler);
    return () => window.removeEventListener("crewpilot:unauthorized", handler);
  }, [queryClient]);

  if (status.isPending) {
    return (
      <div className="p-8">
        <LoadingState />
      </div>
    );
  }

  // 状态接口异常时放行（不因认证检查阻塞应用）
  if (status.isError || status.data === undefined) {
    return <>{children}</>;
  }

  if (status.data.enabled && !status.data.authenticated) {
    return <LoginPage />;
  }

  return <>{children}</>;
}
