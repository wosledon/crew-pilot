import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { formatDateTime } from "@/lib/format";
import { useProjects } from "@/features/projects/api";
import { useAuditLogs } from "./api";

/** 审计日志：敏感操作只读时间线。 */
export function AuditPage() {
  const { t } = useTranslation(["audit", "common"]);
  const projects = useProjects();
  const [projectId, setProjectId] = useState("");
  const logs = useAuditLogs(projectId === "" ? undefined : projectId);

  if (logs.isPending) {
    return <LoadingState />;
  }

  if (logs.isError) {
    return <ErrorState onRetry={() => void logs.refetch()} />;
  }

  const items = logs.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-normal">{t("audit:title")}</h1>
          <p className="text-sm text-muted-foreground">{t("audit:description")}</p>
        </div>
        <Select
          value={projectId}
          onValueChange={setProjectId}
          options={[
            { value: "", label: t("audit:allProjects") },
            ...(projects.data ?? []).map((project) => ({
              value: project.id,
              label: project.name,
            })),
          ]}
          ariaLabel={t("audit:filterProject")}
          className="w-56"
        />
      </div>

      {items.length === 0 ? (
        <EmptyState>{t("audit:empty")}</EmptyState>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>{t("audit:title")}</CardTitle>
          </CardHeader>
          <CardContent>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-muted-foreground">
                  <th className="pb-2 font-medium">{t("audit:createdAt")}</th>
                  <th className="pb-2 font-medium">{t("audit:action")}</th>
                  <th className="pb-2 font-medium">{t("audit:target")}</th>
                  <th className="pb-2 font-medium">{t("audit:actor")}</th>
                  <th className="pb-2 font-medium">{t("audit:detail")}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((log) => (
                  <tr key={log.id} className="border-t border-border">
                    <td className="py-1.5 text-xs text-muted-foreground">
                      {formatDateTime(log.createdAt)}
                    </td>
                    <td className="py-1.5">{log.action}</td>
                    <td className="py-1.5 text-xs text-muted-foreground">
                      {log.targetType}
                      {log.targetId ? ` · ${log.targetId.slice(0, 8)}` : ""}
                    </td>
                    <td className="py-1.5">{log.actor}</td>
                    <td className="max-w-72 truncate py-1.5 text-xs text-muted-foreground">
                      {log.detail ?? "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
