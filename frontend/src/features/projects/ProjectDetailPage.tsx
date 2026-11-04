import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Plus } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { ProjectSubNav } from "@/components/layout/ProjectSubNav";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { CreateWorkItemDialog } from "./components/CreateWorkItemDialog";
import { LogWorkDialog } from "./components/LogWorkDialog";
import { WorkItemCard } from "./components/WorkItemCard";
import { AssignAgentDialog, AgentRunPanel } from "@/features/agents/components/AgentRunPanel";
import {
  useAddWorkLog,
  useCreateWorkItem,
  useProject,
  useTransitionWorkItem,
  useWorkItems,
} from "./api";
import type { WorkItemListItem } from "@/lib/types";

export function ProjectDetailPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["projects", "workitems", "common", "agentruns"]);
  const project = useProject(projectId);
  const [typeFilter, setTypeFilter] = useState("");
  const workItems = useWorkItems(projectId, typeFilter || undefined);
  const createWorkItem = useCreateWorkItem(projectId);
  const transitionWorkItem = useTransitionWorkItem();
  const addWorkLog = useAddWorkLog();

  const [createOpen, setCreateOpen] = useState(false);
  const [logTarget, setLogTarget] = useState<WorkItemListItem | null>(null);
  const [assignTarget, setAssignTarget] = useState<WorkItemListItem | null>(null);

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
            {project.data.key} · {project.data.workItemCount}{" "}
            {t("projects:detail.workItems")}
          </p>
        </div>
      </div>

      <ProjectSubNav projectId={projectId} />

      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={typeFilter}
          onValueChange={setTypeFilter}
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
      </div>

      {items.length === 0 ? (
        <EmptyState>{t("workitems:empty")}</EmptyState>
      ) : (
        <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(340px,1fr))]">
          {items.map((item) => (
            <WorkItemCard
              key={item.id}
              item={item}
              onTransition={(id, toStatus) => void handleTransition(id, toStatus)}
              onLogWork={setLogTarget}
              onAssign={setAssignTarget}
              transitioning={transitionWorkItem.isPending}
            />
          ))}
        </div>
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

      <section className="space-y-3">
        <h2 className="text-base font-semibold">{t("agentruns:title")}</h2>
        <AgentRunPanel projectId={projectId} />
      </section>
    </div>
  );
}
