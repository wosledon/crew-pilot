import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  closestCorners,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useAgentActivity } from "@/features/agents/api";
import { useTransitionWorkItem, useWorkItems } from "./api";
import { nextStatuses, priorityTone } from "./statusFlow";
import type { WorkItemAgentActivity, WorkItemListItem, WorkItemType } from "@/lib/types";
import { cn } from "@/lib/cn";

const activityTones: Record<string, BadgeTone> = {
  queued: "neutral",
  running: "info",
  waitingforhuman: "warning",
  succeeded: "success",
  failed: "danger",
  canceled: "neutral",
};

const requirementColumns = ["backlog", "ready", "inprogress", "inreview", "done"];
const defectColumns = ["new", "confirmed", "fixing", "pendingverification", "closed"];

function columnsFor(type: WorkItemType): string[] {
  return type === "defect" ? defectColumns : requirementColumns;
}

function DraggableCard({
  item,
  activity,
}: {
  item: WorkItemListItem;
  activity?: WorkItemAgentActivity;
}) {
  const { t } = useTranslation(["workitems", "agentruns"]);
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: item.id,
  });

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className={cn(
        "cursor-grab rounded-xl bg-card p-3 shadow-elevation-1 transition-shadow active:cursor-grabbing",
        isDragging && "opacity-50",
      )}
    >
      <p className="text-sm font-medium">{item.title}</p>
      <div className="mt-2 flex items-center gap-2">
        <Badge tone={priorityTone(item.priority)}>
          {t(`workitems:priorities.${item.priority}`)}
        </Badge>
        <span className="text-xs text-muted-foreground">
          {t("workitems:hoursSummary", {
            logged: item.loggedHours,
            estimated: item.estimatedHours ?? "—",
          })}
        </span>
      </div>
      {item.assigneeName ? (
        <p className="mt-1 text-xs text-muted-foreground">@{item.assigneeName}</p>
      ) : null}
      {item.tags ? (
        <p className="mt-1 truncate text-xs text-muted-foreground">#{item.tags}</p>
      ) : null}
      {activity ? (
        <div className="mt-2 flex items-center gap-1.5">
          <Badge tone={activityTones[activity.status] ?? "neutral"}>🤖 {activity.agentName}</Badge>
          <span className="text-xs text-muted-foreground">
            {t(`agentruns:statuses.${activity.status}`)}
          </span>
        </div>
      ) : null}
    </div>
  );
}

function Column({
  status,
  items,
  activityByItem,
}: {
  status: string;
  items: WorkItemListItem[];
  activityByItem: Map<string, WorkItemAgentActivity>;
}) {
  const { t } = useTranslation(["workitems", "kanban"]);
  const { setNodeRef, isOver } = useDroppable({ id: status });
  const wipLimit = 5;
  const isInProgress = status.toLowerCase().includes("progress");

  return (
    <div
      ref={setNodeRef}
      className={cn(
        "flex min-h-40 w-64 shrink-0 flex-col gap-2 rounded-xl p-2 transition-colors",
        isOver ? "bg-secondary-container ring-2 ring-primary" : "bg-surface-container-low",
      )}
    >
      <div className="flex items-center justify-between px-1">
        <span className="text-sm font-medium">
          {t(`workitems:statuses.${status.toLowerCase()}`)}
        </span>
        {isInProgress && items.length > wipLimit ? (
          <Badge tone="danger">
            {t("kanban:wipExceeded", { count: items.length, limit: wipLimit })}
          </Badge>
        ) : (
          <span className="text-xs text-muted-foreground">
            {isInProgress ? `${items.length} / ${wipLimit}` : items.length}
          </span>
        )}
      </div>
      {items.length === 0 ? (
        <p className="px-1 py-4 text-center text-xs text-muted-foreground">
          {t("kanban:emptyColumn")}
        </p>
      ) : (
        items.map((item) => (
          <DraggableCard key={item.id} item={item} activity={activityByItem.get(item.id)} />
        ))
      )}
    </div>
  );
}

/** 看板：dnd-kit 拖拽流转，非法流转由前端状态表与后端状态机双重拦截 */
export function BoardPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["kanban", "workitems", "common"]);
  const [type, setType] = useState<WorkItemType>("requirement");
  const [tag, setTag] = useState("");
  const [assigneeFilter, setAssigneeFilter] = useState("");
  const [priorityFilter, setPriorityFilter] = useState("");
  const [activeId, setActiveId] = useState<string | null>(null);
  const allWorkItems = useWorkItems(projectId, type);
  const workItems = useWorkItems(projectId, type, tag || undefined);
  const activity = useAgentActivity(projectId);
  const activityByItem = new Map((activity.data ?? []).map((entry) => [entry.workItemId, entry]));
  const transitionWorkItem = useTransitionWorkItem();

  const tags = [
    ...new Set(
      (allWorkItems.data ?? [])
        .flatMap((item) => item.tags?.split(",") ?? [])
        .map((value) => value.trim())
        .filter((value) => value !== ""),
    ),
  ].sort((a, b) => a.localeCompare(b));

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }));

  const assignees = [
    ...new Set(
      (allWorkItems.data ?? [])
        .map((item) => item.assigneeName)
        .filter((value): value is string => value !== null && value !== ""),
    ),
  ].sort((a, b) => a.localeCompare(b));
  const priorityOptions = [...new Set((allWorkItems.data ?? []).map((item) => item.priority))];

  const items = (workItems.data ?? []).filter(
    (item) =>
      (assigneeFilter === "" || item.assigneeName === assigneeFilter) &&
      (priorityFilter === "" || item.priority === priorityFilter),
  );
  const columns = columnsFor(type);

  const handleDragStart = (event: DragStartEvent) => {
    setActiveId(String(event.active.id));
  };

  const handleDragEnd = (event: DragEndEvent) => {
    setActiveId(null);
    const overId = event.over?.id;
    if (overId === undefined) {
      return;
    }

    const toStatus = String(overId);
    const item = items.find((candidate) => candidate.id === event.active.id);
    if (item === undefined || item.status === toStatus) {
      return;
    }

    if (!nextStatuses(item.type, item.status).includes(toStatus)) {
      toast.error(t("kanban:invalidTransition"));
      return;
    }

    void transitionWorkItem
      .mutateAsync({ id: item.id, toStatus })
      .then(() => toast.success(t("workitems:statusUpdated")))
      .catch((error: unknown) =>
        toast.error(error instanceof Error ? error.message : t("common:error")),
      );
  };

  const activeItem = items.find((item) => item.id === activeId) ?? null;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-normal">{t("kanban:title")}</h1>
        <div className="flex items-center gap-2">
          <Select
            value={tag}
            onValueChange={setTag}
            options={[
              { value: "", label: t("kanban:allTags") },
              ...tags.map((value) => ({ value, label: value })),
            ]}
            placeholder={t("kanban:filterTag")}
            ariaLabel={t("kanban:filterTag")}
            className="w-40"
          />
          <Select
            value={assigneeFilter}
            onValueChange={setAssigneeFilter}
            options={[
              { value: "", label: t("kanban:allAssignees") },
              ...assignees.map((value) => ({ value, label: `@${value}` })),
            ]}
            placeholder={t("kanban:filterAssignee")}
            ariaLabel={t("kanban:filterAssignee")}
            className="w-40"
          />
          <Select
            value={priorityFilter}
            onValueChange={setPriorityFilter}
            options={[
              { value: "", label: t("kanban:allPriorities") },
              ...priorityOptions.map((value) => ({
                value,
                label: t(`workitems:priorities.${value}`),
              })),
            ]}
            placeholder={t("kanban:filterPriority")}
            ariaLabel={t("kanban:filterPriority")}
            className="w-32"
          />
          <Select
            value={type}
            onValueChange={(value) => {
              setType(value as WorkItemType);
              setTag("");
              setAssigneeFilter("");
              setPriorityFilter("");
            }}
            options={[
              { value: "requirement", label: t("kanban:types.requirement") },
              { value: "defect", label: t("kanban:types.defect") },
            ]}
            ariaLabel={t("kanban:filterType")}
            className="w-36"
          />
        </div>
      </div>

      {workItems.isPending ? (
        <LoadingState />
      ) : workItems.isError ? (
        <ErrorState onRetry={() => void workItems.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState>{t("kanban:noItems")}</EmptyState>
      ) : (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCorners}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
        >
          <div className="flex gap-3 overflow-x-auto pb-2">
            {columns.map((status) => (
              <Column
                key={status}
                status={status}
                items={items.filter((item) => item.status === status)}
                activityByItem={activityByItem}
              />
            ))}
          </div>
          <DragOverlay>
            {activeItem ? (
              <div className="rounded-md border border-primary bg-card p-3 shadow-lg">
                <p className="text-sm font-medium">{activeItem.title}</p>
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>
      )}
    </div>
  );
}
