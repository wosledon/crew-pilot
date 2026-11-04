import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Check, LayoutGrid, List, Plus, Trash2, X } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Input } from "@/components/ui/Input";
import { ProjectSubNav } from "@/components/layout/ProjectSubNav";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { CreateWorkItemDialog } from "./components/CreateWorkItemDialog";
import { EditWorkItemDialog } from "./components/EditWorkItemDialog";
import { LogWorkDialog } from "./components/LogWorkDialog";
import { ProjectMembersCard } from "./components/ProjectMembersCard";
import { ProjectOverview } from "./components/ProjectOverview";
import { WorkItemCard } from "./components/WorkItemCard";
import { WorkItemDetailDialog } from "./components/WorkItemDetailDialog";
import { ActivityFeed } from "./ActivityFeed";
import { IntegrationCard } from "@/features/integrations/IntegrationCard";
import { ReleasePanel } from "@/features/integrations/ReleasePanel";
import { WorkflowPanel } from "@/features/workflows/WorkflowPanel";
import { AssignAgentDialog, AgentRunPanel } from "@/features/agents/components/AgentRunPanel";
import { AgentTeamCard } from "@/features/agents/components/AgentTeamCard";
import {
  useAddWorkLog,
  useBatchWorkItems,
  useCreateWorkItem,
  useProject,
  useTransitionWorkItem,
  useWorkItems,
} from "./api";
import { nextStatuses, priorityTone, statusTone } from "./statusFlow";
import type { WorkItemDetail, WorkItemListItem } from "@/lib/types";

export function ProjectDetailPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["projects", "workitems", "common", "agentruns"]);
  const project = useProject(projectId);
  const [typeFilter, setTypeFilter] = useState("");
  const workItems = useWorkItems(projectId, typeFilter || undefined);
  const createWorkItem = useCreateWorkItem(projectId);
  const transitionWorkItem = useTransitionWorkItem();
  const batchWorkItems = useBatchWorkItems(projectId);
  const addWorkLog = useAddWorkLog();
  const confirm = useConfirm();

  const [view, setView] = useState<"cards" | "list">("cards");
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [batchStatus, setBatchStatus] = useState("");
  const [batchAssignee, setBatchAssignee] = useState("");

  const [createOpen, setCreateOpen] = useState(false);
  const [logTarget, setLogTarget] = useState<WorkItemListItem | null>(null);
  const [assignTarget, setAssignTarget] = useState<WorkItemListItem | null>(null);
  const [detailTarget, setDetailTarget] = useState<WorkItemListItem | null>(null);
  const [editTarget, setEditTarget] = useState<WorkItemDetail | null>(null);

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleTransition = async (id: string, toStatus: string) => {
    try {
      await transitionWorkItem.mutateAsync({ id, toStatus });
      toast.success(t("workitems:statusUpdated"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleLogWork = async (body: { hours: number; note?: string }) => {
    if (logTarget === null) {
      return;
    }
    try {
      await addWorkLog.mutateAsync({ workItemId: logTarget.id, ...body });
      toast.success(t("workitems:workLogged"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const toggleSelected = (id: string) =>
    setSelectedIds((previous) =>
      previous.includes(id) ? previous.filter((value) => value !== id) : [...previous, id],
    );

  const selectedItems = (workItems.data ?? []).filter((item) => selectedIds.includes(item.id));

  const availableStatuses = [
    ...new Set(selectedItems.flatMap((item) => nextStatuses(item.type, item.status))),
  ];

  const notifyBatchResult = (succeeded: number, failed: number) => {
    if (failed === 0) {
      toast.success(t("workitems:batchResult", { succeeded, failed }));
    } else {
      toast.error(t("workitems:batchPartial", { succeeded, failed }));
    }
    setSelectedIds([]);
    setBatchStatus("");
  };

  const handleBatchStatus = async () => {
    if (batchStatus === "") {
      return;
    }
    try {
      const result = await batchWorkItems.mutateAsync({
        ids: selectedIds,
        action: "status",
        toStatus: batchStatus,
      });
      notifyBatchResult(result.succeeded, result.failed);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleBatchAssign = async () => {
    try {
      const result = await batchWorkItems.mutateAsync({
        ids: selectedIds,
        action: "assign",
        assigneeName: batchAssignee.trim(),
      });
      notifyBatchResult(result.succeeded, result.failed);
      setBatchAssignee("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleBatchDelete = async () => {
    const approved = await confirm({
      title: t("workitems:batchDelete"),
      description: t("workitems:batchDeleteConfirm", { count: selectedIds.length }),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      const result = await batchWorkItems.mutateAsync({
        ids: selectedIds,
        action: "delete",
      });
      notifyBatchResult(result.succeeded, result.failed);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (project.isPending || workItems.isPending) {
    return <LoadingState />;
  }

  if (project.isError || workItems.isError) {
    return (
      <ErrorState
        onRetry={() => {
          void project.refetch();
          void workItems.refetch();
        }}
      />
    );
  }

  const items = workItems.data;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link
          to="/projects"
          aria-label={t("projects:detail.back")}
          className="inline-flex h-9 w-9 items-center justify-center rounded-md hover:bg-muted"
        >
          <ArrowLeft className="h-4 w-4" />
        </Link>
        <div>
          <h1 className="text-xl font-semibold">{project.data.name}</h1>
          <p className="text-sm text-muted-foreground">
            {project.data.key} · {project.data.workItemCount} {t("projects:detail.workItems")}
          </p>
        </div>
      </div>

      <ProjectSubNav projectId={projectId} />

      <ProjectOverview projectId={projectId} />

      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={typeFilter}
          onValueChange={(value) => {
            setTypeFilter(value);
            setSelectedIds([]);
          }}
          options={[
            { value: "", label: `${t("projects:detail.filterType")}: ${t("projects:detail.all")}` },
            { value: "requirement", label: t("workitems:types.requirement") },
            { value: "defect", label: t("workitems:types.defect") },
            { value: "task", label: t("workitems:types.task") },
          ]}
          ariaLabel={t("projects:detail.filterType")}
          className="w-44"
        />
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" />
          {t("workitems:create")}
        </Button>
        <div className="ml-auto flex items-center gap-1 rounded-md border border-border p-0.5">
          <Button
            size="sm"
            variant={view === "cards" ? "primary" : "ghost"}
            aria-label={t("workitems:viewCards")}
            onClick={() => setView("cards")}
          >
            <LayoutGrid className="h-3.5 w-3.5" />
            {t("workitems:viewCards")}
          </Button>
          <Button
            size="sm"
            variant={view === "list" ? "primary" : "ghost"}
            aria-label={t("workitems:viewList")}
            onClick={() => setView("list")}
          >
            <List className="h-3.5 w-3.5" />
            {t("workitems:viewList")}
          </Button>
        </div>
      </div>

      {selectedIds.length > 0 ? (
        <div className="flex flex-wrap items-end gap-2 rounded-lg border border-border bg-card p-3">
          <span className="pb-2 text-sm font-medium">
            {t("workitems:selected", { count: selectedIds.length })}
          </span>
          <div className="flex items-end gap-2">
            <Select
              value={batchStatus}
              onValueChange={setBatchStatus}
              options={availableStatuses.map((status) => ({
                value: status,
                label: t(`workitems:statuses.${status.toLowerCase()}`),
              }))}
              placeholder={t("workitems:batchStatus")}
              ariaLabel={t("workitems:batchStatus")}
              className="w-40"
            />
            <Button
              variant="outline"
              onClick={() => void handleBatchStatus()}
              disabled={batchStatus === "" || batchWorkItems.isPending}
            >
              {t("workitems:batchApplyStatus")}
            </Button>
          </div>
          <div className="flex items-end gap-2">
            <Input
              value={batchAssignee}
              onChange={(event) => setBatchAssignee(event.target.value)}
              placeholder={t("workitems:batchAssigneePlaceholder")}
              className="w-40"
            />
            <Button
              variant="outline"
              onClick={() => void handleBatchAssign()}
              disabled={batchWorkItems.isPending}
            >
              {t("workitems:batchAssign")}
            </Button>
          </div>
          <Button
            variant="destructive"
            onClick={() => void handleBatchDelete()}
            disabled={batchWorkItems.isPending}
          >
            <Trash2 className="h-4 w-4" />
            {t("common:delete")}
          </Button>
          <Button
            variant="ghost"
            size="icon"
            aria-label={t("common:cancel")}
            onClick={() => setSelectedIds([])}
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      ) : null}

      {items.length === 0 ? (
        <EmptyState>{t("workitems:empty")}</EmptyState>
      ) : view === "cards" ? (
        <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(340px,1fr))]">
          {items.map((item) => (
            <WorkItemCard
              key={item.id}
              item={item}
              onTransition={(id, toStatus) => void handleTransition(id, toStatus)}
              onLogWork={setLogTarget}
              onAssign={setAssignTarget}
              onOpenDetail={setDetailTarget}
              transitioning={transitionWorkItem.isPending}
            />
          ))}
        </div>
      ) : (
        <Card>
          <CardContent className="overflow-x-auto p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-xs text-muted-foreground">
                  <th className="p-2">
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={t("workitems:selectAll")}
                      onClick={() =>
                        setSelectedIds(
                          selectedIds.length === items.length ? [] : items.map((item) => item.id),
                        )
                      }
                    >
                      {selectedIds.length === items.length ? (
                        <Check className="h-4 w-4" />
                      ) : (
                        <span className="h-3.5 w-3.5 rounded-sm border border-border" />
                      )}
                    </Button>
                  </th>
                  <th className="p-2 font-medium">{t("workitems:titleLabel")}</th>
                  <th className="p-2 font-medium">{t("workitems:typeLabel")}</th>
                  <th className="p-2 font-medium">{t("workitems:statusLabel")}</th>
                  <th className="p-2 font-medium">{t("workitems:priorityLabel")}</th>
                  <th className="p-2 font-medium">{t("workitems:assigneeLabel")}</th>
                  <th className="p-2 text-right font-medium">{t("workitems:hoursColumn")}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr
                    key={item.id}
                    className="border-b border-border last:border-0 hover:bg-muted/40"
                  >
                    <td className="p-2">
                      <Button
                        size="icon"
                        variant="ghost"
                        aria-label={t("workitems:selectItem")}
                        onClick={() => toggleSelected(item.id)}
                      >
                        {selectedIds.includes(item.id) ? (
                          <Check className="h-4 w-4 text-primary" />
                        ) : (
                          <span className="h-3.5 w-3.5 rounded-sm border border-border" />
                        )}
                      </Button>
                    </td>
                    <td className="p-2">
                      <button
                        type="button"
                        className="max-w-72 truncate text-left hover:underline"
                        onClick={() => setDetailTarget(item)}
                      >
                        {item.title}
                      </button>
                    </td>
                    <td className="p-2">
                      <Badge tone="neutral">{t(`workitems:types.${item.type}`)}</Badge>
                    </td>
                    <td className="p-2">
                      <Badge tone={statusTone(item.status)}>
                        {t(`workitems:statuses.${item.status.toLowerCase()}`)}
                      </Badge>
                    </td>
                    <td className="p-2">
                      <Badge tone={priorityTone(item.priority)}>
                        {t(`workitems:priorities.${item.priority}`)}
                      </Badge>
                    </td>
                    <td className="p-2 text-muted-foreground">{item.assigneeName ?? "—"}</td>
                    <td className="p-2 text-right text-muted-foreground">
                      {item.loggedHours} / {item.estimatedHours ?? "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}

      <CreateWorkItemDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onSubmit={async (body) => {
          try {
            await createWorkItem.mutateAsync(body);
            toast.success(t("workitems:created"));
          } catch (error) {
            toast.error(errorMessage(error));
          }
        }}
      />

      {logTarget ? (
        <LogWorkDialog
          open
          onOpenChange={(open) => {
            if (!open) {
              setLogTarget(null);
            }
          }}
          workItemTitle={logTarget.title}
          onSubmit={handleLogWork}
        />
      ) : null}

      {assignTarget ? (
        <AssignAgentDialog
          open
          onOpenChange={(open) => {
            if (!open) {
              setAssignTarget(null);
            }
          }}
          workItemId={assignTarget.id}
        />
      ) : null}

      {detailTarget ? (
        <WorkItemDetailDialog
          open
          workItemId={detailTarget.id}
          onOpenChange={(open) => {
            if (!open) {
              setDetailTarget(null);
            }
          }}
          onEdit={(detail) => {
            setDetailTarget(null);
            setEditTarget(detail);
          }}
        />
      ) : null}

      {editTarget ? (
        <EditWorkItemDialog
          open
          item={editTarget}
          projectId={projectId}
          onOpenChange={(open) => {
            if (!open) {
              setEditTarget(null);
            }
          }}
        />
      ) : null}

      <div className="grid gap-4 lg:grid-cols-2">
        <ProjectMembersCard projectId={projectId} />
        <AgentTeamCard projectId={projectId} defaultTeamId={project.data.defaultTeamId} />
        <ActivityFeed projectId={projectId} />
      </div>

      <WorkflowPanel projectId={projectId} />

      <div className="grid gap-4 lg:grid-cols-2">
        <IntegrationCard projectId={projectId} />
        <ReleasePanel projectId={projectId} />
      </div>

      <section className="space-y-3">
        <h2 className="text-base font-semibold">{t("agentruns:title")}</h2>
        <AgentRunPanel projectId={projectId} />
      </section>
    </div>
  );
}
