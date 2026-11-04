import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Bot } from "lucide-react";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Dialog } from "@/components/ui/Dialog";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useAgentRuns, useAgents, useAssignAgent, useCancelAgentRun } from "../api";
import { AgentRunDetailDialog } from "./AgentRunDetailDialog";

const statusTones: Record<string, BadgeTone> = {
  queued: "neutral",
  running: "info",
  succeeded: "success",
  failed: "danger",
  canceled: "warning",
};

/** 指派工作项给 Agent 的对话框 */
export function AssignAgentDialog({
  open,
  onOpenChange,
  workItemId,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  workItemId: string;
}) {
  const { t } = useTranslation(["agentruns", "agents", "common"]);
  const agents = useAgents();
  const assignAgent = useAssignAgent();
  const [selected, setSelected] = useSelectedAgent(agents.data);

  const enabledAgents = (agents.data ?? []).filter((agent) => agent.enabled);

  const handleAssign = async () => {
    if (selected === "") {
      return;
    }
    try {
      await assignAgent.mutateAsync({ workItemId, agentId: selected });
      toast.success(t("agentruns:assigned"));
      onOpenChange(false);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("agentruns:assign")}
      closeLabel={t("common:cancel")}
    >
      {agents.isPending ? (
        <LoadingState />
      ) : enabledAgents.length === 0 ? (
        <EmptyState>{t("agents:empty")}</EmptyState>
      ) : (
        <div className="space-y-4">
          <Select
            value={selected}
            onValueChange={setSelected}
            options={enabledAgents.map((agent) => ({
              value: agent.id,
              label: `${agent.name}（${t(`agents:jobs.${agent.job}`)} · ${agent.model}）`,
            }))}
            placeholder={t("agentruns:selectAgent")}
            ariaLabel={t("agentruns:selectAgent")}
          />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleAssign()}
              disabled={assignAgent.isPending || selected === ""}
            >
              <Bot className="h-4 w-4" />
              {t("agentruns:assign")}
            </Button>
          </div>
        </div>
      )}
    </Dialog>
  );
}

/** 执行记录列表：按工作项或按项目过滤（含取消接管） */
export function AgentRunPanel({
  workItemId,
  projectId,
}: {
  workItemId?: string;
  projectId?: string;
}) {
  const { t } = useTranslation(["agentruns", "common"]);
  const runs = useAgentRuns({ workItemId, projectId });
  const cancelRun = useCancelAgentRun();
  const [detailRunId, setDetailRunId] = useState<string | null>(null);

  const handleCancel = async (id: string) => {
    try {
      await cancelRun.mutateAsync(id);
      toast.success(t("agentruns:canceled"));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  if (runs.isPending) {
    return <LoadingState />;
  }

  if (runs.isError) {
    return <ErrorState onRetry={() => void runs.refetch()} />;
  }

  const items = runs.data ?? [];
  if (items.length === 0) {
    return <EmptyState>{t("agentruns:empty")}</EmptyState>;
  }

  return (
    <>
      <ul className="space-y-2">
        {items.map((run) => (
          <li key={run.id}>
            <Card>
              <CardHeader className="flex-row items-center justify-between gap-2 py-3">
                <CardTitle className="flex min-w-0 items-center gap-2 text-sm">
                  <Bot className="h-4 w-4 shrink-0 text-primary" />
                  <span className="truncate">{run.agentName}</span>
                  <Badge tone={statusTones[run.status] ?? "neutral"}>
                    {t(`agentruns:statuses.${run.status}`)}
                  </Badge>
                  <Badge tone="neutral">{t(`agentruns:triggers.${run.trigger}`)}</Badge>
                </CardTitle>
                <div className="flex shrink-0 items-center gap-2">
                  <span className="text-xs text-muted-foreground">
                    {new Date(run.createdAt).toLocaleString()}
                  </span>
                  {run.duration !== null ? (
                    <span className="text-xs text-muted-foreground">
                      {formatDuration(run.duration)}
                    </span>
                  ) : null}
                  <Button size="sm" variant="outline" onClick={() => setDetailRunId(run.id)}>
                    {t("agentruns:viewDetail")}
                  </Button>
                  {run.status === "queued" || run.status === "running" ? (
                    <Button size="sm" variant="outline" onClick={() => void handleCancel(run.id)}>
                      {t("agentruns:cancel")}
                    </Button>
                  ) : null}
                </div>
              </CardHeader>
              {run.error !== null ? (
                <CardContent className="pt-0 text-xs text-destructive">{run.error}</CardContent>
              ) : null}
            </Card>
          </li>
        ))}
      </ul>
      <AgentRunDetailDialog
        runId={detailRunId}
        open={detailRunId !== null}
        onOpenChange={(open) => {
          if (!open) {
            setDetailRunId(null);
          }
        }}
      />
    </>
  );
}

function useSelectedAgent(agents: ReturnType<typeof useAgents>["data"]) {
  const [selected, setSelected] = useState<string>("");
  if (selected === "" && agents && agents.length > 0) {
    const first = agents.find((agent) => agent.enabled);
    if (first) {
      setSelected(first.id);
    }
  }
  return [selected, setSelected] as const;
}

function formatDuration(iso: string): string {
  // TimeSpan 序列化为 [-]hh:mm:ss.fffffff
  const match = /^(\d+):(\d{2}):(\d{2})/.exec(iso);
  if (match === null) {
    return iso;
  }
  const [, hours, minutes, seconds] = match;
  return `${hours}h ${minutes}m ${seconds}s`;
}
