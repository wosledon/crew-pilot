import { useTranslation } from "react-i18next";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { formatDateTime } from "@/lib/format";
import type { WorkflowRunStatus } from "@/lib/types";
import { useCancelWorkflowRun, useWorkflowRun } from "./api";

const runStatusTones: Record<WorkflowRunStatus, BadgeTone> = {
  running: "info",
  waitingforhuman: "warning",
  succeeded: "success",
  failed: "danger",
  canceled: "neutral",
};

export interface WorkflowRunDialogProps {
  runId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** 工作流运行详情：执行状态、Agent 步骤记录与卡点历史。 */
export function WorkflowRunDialog({ runId, open, onOpenChange }: WorkflowRunDialogProps) {
  const { t } = useTranslation(["workflows", "agentruns", "common"]);
  const detail = useWorkflowRun(runId);
  const cancelRun = useCancelWorkflowRun();
  const confirm = useConfirm();

  const handleCancel = async () => {
    const approved = await confirm({
      title: t("workflows:cancelRun"),
      description: t("workflows:blockerQueueHint"),
      confirmText: t("workflows:cancelRun"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await cancelRun.mutateAsync(runId);
      toast.success(t("workflows:runCanceled"));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  const run = detail.data?.run;
  const canCancel =
    run !== undefined && (run.status === "running" || run.status === "waitingforhuman");

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("workflows:runDetail")}
      closeLabel={t("common:cancel")}
      className="max-w-2xl"
    >
      {detail.isPending ? (
        <LoadingState />
      ) : detail.isError || detail.data === undefined ? (
        <ErrorState onRetry={() => void detail.refetch()} />
      ) : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={runStatusTones[detail.data.run.status]}>
              {t(`workflows:runStatuses.${detail.data.run.status}`)}
            </Badge>
            <span className="text-sm font-medium">{detail.data.run.workflowName}</span>
            <span className="text-xs text-muted-foreground">{detail.data.run.workItemTitle}</span>
            <span className="text-xs text-muted-foreground">
              {t("workflows:currentStep")} {detail.data.run.currentStepOrder + 1}
            </span>
          </div>

          {canCancel ? (
            <Button variant="outline" size="sm" onClick={() => void handleCancel()}>
              {t("workflows:cancelRun")}
            </Button>
          ) : null}

          <section>
            <h4 className="text-sm font-medium">{t("workflows:agentRuns")}</h4>
            {detail.data.agentRuns.length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">{t("workflows:noRuns")}</p>
            ) : (
              <ul className="mt-2 space-y-2">
                {detail.data.agentRuns.map((agentRun) => (
                  <li
                    key={agentRun.id}
                    className="rounded-md border border-border px-3 py-2 text-sm"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <Badge
                          tone={
                            agentRun.status === "succeeded"
                              ? "success"
                              : agentRun.status === "failed"
                                ? "danger"
                                : agentRun.status === "waitingforhuman"
                                  ? "warning"
                                  : "info"
                          }
                        >
                          {t(`agentruns:statuses.${agentRun.status}`, {
                            defaultValue: agentRun.status,
                          })}
                        </Badge>
                        <span className="font-medium">{agentRun.agentName}</span>
                      </div>
                      <span className="text-xs text-muted-foreground">
                        {formatDateTime(agentRun.createdAt)}
                      </span>
                    </div>
                    {agentRun.error ? (
                      <p className="mt-1 text-xs text-destructive">{agentRun.error}</p>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section>
            <h4 className="text-sm font-medium">{t("workflows:blockers")}</h4>
            {detail.data.blockers.length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">{t("workflows:noBlockers")}</p>
            ) : (
              <ul className="mt-2 space-y-2">
                {detail.data.blockers.map((blocker) => (
                  <li
                    key={blocker.id}
                    className="rounded-md border border-border px-3 py-2 text-sm"
                  >
                    <div className="flex items-center gap-2">
                      <Badge tone={blocker.status === "open" ? "warning" : "neutral"}>
                        {t(`workflows:blockerKinds.${blocker.kind}`)}
                      </Badge>
                      <span className="text-xs text-muted-foreground">
                        {t(`workflows:blockerStatuses.${blocker.status}`)}
                      </span>
                    </div>
                    <p className="mt-1">{blocker.reason}</p>
                    {blocker.resolutionNote ? (
                      <p className="mt-1 text-xs text-muted-foreground">
                        {t(`workflows:resolutions.${blocker.resolution ?? "continue"}`)}：
                        {blocker.resolutionNote}
                      </p>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </Dialog>
  );
}
