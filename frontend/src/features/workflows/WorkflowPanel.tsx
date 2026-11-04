import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Trash2, Workflow as WorkflowIcon } from "lucide-react";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useWorkItems } from "@/features/projects/api";
import type { WorkflowRunStatus } from "@/lib/types";
import { formatDateTime } from "@/lib/format";
import {
  useCreateWorkflowFromTemplate,
  useDeleteWorkflow,
  useStartWorkflowRun,
  useWorkflowRuns,
  useWorkflowTemplates,
  useWorkflows,
} from "./api";
import { WorkflowRunDialog } from "./WorkflowRunDialog";

const runStatusTones: Record<WorkflowRunStatus, BadgeTone> = {
  running: "info",
  waitingforhuman: "warning",
  succeeded: "success",
  failed: "danger",
  canceled: "neutral",
};

export interface WorkflowPanelProps {
  projectId: string;
}

/** 项目工作流：模板创建、启动运行与运行记录。 */
export function WorkflowPanel({ projectId }: WorkflowPanelProps) {
  const { t } = useTranslation(["workflows", "workitems", "common"]);
  const workflows = useWorkflows(projectId);
  const templates = useWorkflowTemplates();
  const workItems = useWorkItems(projectId);
  const runs = useWorkflowRuns({ projectId });
  const createFromTemplate = useCreateWorkflowFromTemplate(projectId);
  const deleteWorkflow = useDeleteWorkflow(projectId);
  const startRun = useStartWorkflowRun();
  const confirm = useConfirm();

  const [templateOpen, setTemplateOpen] = useState(false);
  const [templateKey, setTemplateKey] = useState("");
  const [workflowName, setWorkflowName] = useState("");
  const [startWorkflowId, setStartWorkflowId] = useState("");
  const [startWorkItemId, setStartWorkItemId] = useState("");
  const [openRunId, setOpenRunId] = useState<string | null>(null);

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleCreateFromTemplate = async () => {
    if (templateKey === "") {
      return;
    }
    try {
      await createFromTemplate.mutateAsync({
        templateKey,
        name: workflowName.trim() || undefined,
      });
      toast.success(t("workflows:created"));
      setTemplateOpen(false);
      setTemplateKey("");
      setWorkflowName("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleStart = async () => {
    if (startWorkflowId === "" || startWorkItemId === "") {
      return;
    }
    try {
      await startRun.mutateAsync({
        workflowId: startWorkflowId,
        workItemId: startWorkItemId,
      });
      toast.success(t("workflows:started"));
      setStartWorkItemId("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async (id: string) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: t("workflows:deleteConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteWorkflow.mutateAsync(id);
      toast.success(t("workflows:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const workflowList = workflows.data ?? [];
  const workItemOptions = (workItems.data ?? []).map((item) => ({
    value: item.id,
    label: `${t(`workitems:types.${item.type}`)} · ${item.title}`,
  }));

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="flex items-center gap-2">
          <WorkflowIcon className="h-4 w-4" />
          {t("workflows:title")}
        </CardTitle>
        <Button size="sm" variant="outline" onClick={() => setTemplateOpen(true)}>
          {t("workflows:createFromTemplate")}
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {workflows.isPending ? (
          <LoadingState />
        ) : workflows.isError ? (
          <ErrorState onRetry={() => void workflows.refetch()} />
        ) : workflowList.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("workflows:empty")}</p>
        ) : (
          <ul className="space-y-2">
            {workflowList.map((workflow) => (
              <li
                key={workflow.id}
                className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2"
              >
                <div className="min-w-0">
                  <p className="text-sm font-medium">{workflow.name}</p>
                  <p className="truncate text-xs text-muted-foreground">
                    {workflow.steps.map((step) => step.name).join(" → ")}
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-1">
                  <Badge tone="neutral">
                    {workflow.steps.length} {t("workflows:steps")}
                  </Badge>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDelete(workflow.id)}
                  >
                    <Trash2 className="h-3.5 w-3.5 text-destructive" />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}

        {workflowList.length > 0 ? (
          <div className="flex flex-wrap items-end gap-2 border-t border-border pt-3">
            <Select
              value={startWorkflowId}
              onValueChange={setStartWorkflowId}
              options={workflowList.map((workflow) => ({
                value: workflow.id,
                label: workflow.name,
              }))}
              placeholder={t("workflows:selectWorkflow")}
              ariaLabel={t("workflows:selectWorkflow")}
              className="w-48"
            />
            <Select
              value={startWorkItemId}
              onValueChange={setStartWorkItemId}
              options={workItemOptions}
              placeholder={t("workflows:selectWorkItem")}
              ariaLabel={t("workflows:selectWorkItem")}
              className="w-64"
            />
            <Button
              onClick={() => void handleStart()}
              disabled={startWorkflowId === "" || startWorkItemId === "" || startRun.isPending}
            >
              {t("workflows:start")}
            </Button>
          </div>
        ) : null}

        <div>
          <h4 className="text-sm font-medium">{t("workflows:runs")}</h4>
          {runs.isPending ? (
            <LoadingState />
          ) : (runs.data ?? []).length === 0 ? (
            <p className="mt-1 text-sm text-muted-foreground">{t("workflows:noRuns")}</p>
          ) : (
            <ul className="mt-2 divide-y divide-border">
              {(runs.data ?? []).slice(0, 10).map((run) => (
                <li key={run.id}>
                  <button
                    type="button"
                    className="flex w-full items-center justify-between gap-3 py-1.5 text-left text-sm hover:underline"
                    onClick={() => setOpenRunId(run.id)}
                  >
                    <span className="flex min-w-0 items-center gap-2">
                      <Badge tone={runStatusTones[run.status]}>
                        {t(`workflows:runStatuses.${run.status}`)}
                      </Badge>
                      <span className="truncate">{run.workflowName}</span>
                      <span className="truncate text-xs text-muted-foreground">
                        {run.workItemTitle}
                      </span>
                    </span>
                    <span className="shrink-0 text-xs text-muted-foreground">
                      {formatDateTime(run.createdAt)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </CardContent>

      <Dialog
        open={templateOpen}
        onOpenChange={setTemplateOpen}
        title={t("workflows:createTitle")}
        closeLabel={t("common:cancel")}
      >
        <div className="space-y-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workflows:template")}</span>
            <Select
              value={templateKey}
              onValueChange={setTemplateKey}
              options={(templates.data ?? []).map((template) => ({
                value: template.key,
                label: template.name,
              }))}
              ariaLabel={t("workflows:template")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workflows:name")}</span>
            <Input value={workflowName} onChange={(event) => setWorkflowName(event.target.value)} />
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setTemplateOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleCreateFromTemplate()}
              disabled={templateKey === "" || createFromTemplate.isPending}
            >
              {t("common:create")}
            </Button>
          </div>
        </div>
      </Dialog>

      {openRunId !== null ? (
        <WorkflowRunDialog
          open
          runId={openRunId}
          onOpenChange={(open) => {
            if (!open) {
              setOpenRunId(null);
            }
          }}
        />
      ) : null}
    </Card>
  );
}
