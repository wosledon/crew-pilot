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
import { Badge } from "@/components/ui/Badge";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useTransitionWorkItem, useWorkItems } from "./api";
import { nextStatuses, priorityTone } from "./statusFlow";
import type { WorkItemListItem, WorkItemType } from "@/lib/types";
import { cn } from "@/lib/cn";

const requirementColumns = ["backlog", "ready", "inProgress", "inReview", "done"];
const defectColumns = ["new", "confirmed", "fixing", "pendingVerification", "closed"];

function columnsFor(type: WorkItemType): string[] {
  return type === "defect" ? defectColumns : requirementColumns;
}

function DraggableCard({ item }: { item: WorkItemListItem }) {
  const { t } = useTranslation("workitems");
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: item.id,
  });

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className={cn(
        "cursor-grab rounded-md border border-border bg-card p-3 shadow-sm active:cursor-grabbing",
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
    </div>
  );
}

function Column({
  status,
  items,
}: {
  status: string;
  items: WorkItemListItem[];
}) {
  const { t } = useTranslation(["workitems", "kanban"]);
  const { setNodeRef, isOver } = useDroppable({ id: status });

  return (
    <div
      ref={setNodeRef}
      className={cn(
        "flex min-h-40 w-64 shrink-0 flex-col gap-2 rounded-lg border border-border p-2 transition-colors",
        isOver ? "border-primary bg-accent" : "bg-muted/40",
      )}
    >
      <div className="flex items-center justify-between px-1">
        <span className="text-sm font-medium">
          {t(`workitems:statuses.${status.toLowerCase()}`)}
        </span>
        <span className="text-xs text-muted-foreground">{items.length}</span>
      </div>
      {items.length === 0 ? (
        <p className="px-1 py-4 text-center text-xs text-muted-foreground">
          {t("kanban:emptyColumn")}
        </p>
      ) : (
        items.map((item) => <DraggableCard key={item.id} item={item} />)
      )}
    </div>
  );
}

/** 看板：dnd-kit 拖拽流转，非法流转由前端状态表与后端状态机双重拦截 */
export function BoardPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["kanban", "workitems", "common"]);
  const [type, setType] = useState<WorkItemType>("requirement");
  const [activeId, setActiveId] = useState<string | null>(null);
  const workItems = useWorkItems(projectId, type);
  const transitionWorkItem = useTransitionWorkItem();

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
  );

  const items = workItems.data ?? [];
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
        <h1 className="text-xl font-semibold">{t("kanban:title")}</h1>
        <Select
          value={type}
          onValueChange={(value) => setType(value as WorkItemType)}
          options={[
            { value: "requirement", label: t("kanban:types.requirement") },
            { value: "defect", label: t("kanban:types.defect") },
          ]}
          ariaLabel={t("kanban:filterType")}
          className="w-36"
        />
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
