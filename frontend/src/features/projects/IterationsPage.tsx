import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Pencil, Play, Plus, Square, Trash2 } from "lucide-react";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { Textarea } from "@/components/ui/Input";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { Select } from "@/components/ui/Select";
import { toast } from "@/components/ui/Toast";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { BurndownChart } from "./BurndownChart";
import {
  useBurndown,
  useCompleteIteration,
  useCreateIteration,
  useDeleteIteration,
  useIterations,
  useMilestones,
  useStartIteration,
  useUpdateIteration,
} from "./api";
import type { IterationListItem, IterationStatus } from "@/lib/types";

const statusTones: Record<IterationStatus, BadgeTone> = {
  planned: "neutral",
  active: "info",
  completed: "success",
};

/** 迭代管理：列表 + 新建 + 开始/完成 + 燃尽图 */
export function IterationsPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["iterations", "common"]);
  const iterations = useIterations(projectId);
  const milestones = useMilestones(projectId);
  const createIteration = useCreateIteration(projectId);
  const updateIteration = useUpdateIteration(projectId);
  const startIteration = useStartIteration();
  const completeIteration = useCompleteIteration();
  const deleteIteration = useDeleteIteration(projectId);
  const confirm = useConfirm();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [goal, setGoal] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [milestoneId, setMilestoneId] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const activeIteration = (iterations.data ?? []).find(
    (iteration) => iteration.status === "active",
  );
  const burndown = useBurndown(activeIteration?.id);

  const resetForm = () => {
    setName("");
    setGoal("");
    setStartDate("");
    setEndDate("");
    setMilestoneId("");
    setEditingId(null);
  };

  const openCreate = () => {
    resetForm();
    setDialogOpen(true);
  };

  const openEdit = (iteration: IterationListItem) => {
    setEditingId(iteration.id);
    setName(iteration.name);
    setGoal(iteration.goal ?? "");
    setStartDate(iteration.startDate);
    setEndDate(iteration.endDate);
    setMilestoneId(iteration.milestoneId ?? "");
    setDialogOpen(true);
  };

  const handleSubmit = async () => {
    if (endDate !== "" && startDate !== "" && endDate < startDate) {
      toast.error(t("iterations:invalidDates"));
      return;
    }
    const body = {
      name: name.trim(),
      goal: goal.trim() || undefined,
      startDate,
      endDate,
      milestoneId: milestoneId || undefined,
    };
    try {
      if (editingId === null) {
        await createIteration.mutateAsync(body);
        toast.success(t("iterations:created"));
      } else {
        await updateIteration.mutateAsync({ id: editingId, body });
        toast.success(t("iterations:updated"));
      }
      setDialogOpen(false);
      resetForm();
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async (id: string) => {
    const approved = await confirm({
      title: t("iterations:deleteConfirm.title"),
      description: t("iterations:deleteConfirm.description"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteIteration.mutateAsync(id);
      toast.success(t("iterations:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (iterations.isPending) {
    return <LoadingState />;
  }

  if (iterations.isError) {
    return <ErrorState onRetry={() => void iterations.refetch()} />;
  }

  const items = iterations.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-normal">{t("iterations:title")}</h1>
          <p className="text-sm text-muted-foreground">{t("iterations:description")}</p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="h-4 w-4" />
          {t("iterations:create")}
        </Button>
      </div>

      {items.length === 0 ? (
        <EmptyState>{t("iterations:empty")}</EmptyState>
      ) : (
        <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(300px,1fr))]">
          {items.map((iteration) => (
            <Card key={iteration.id}>
              <CardHeader>
                <CardTitle className="flex items-center justify-between gap-2">
                  <span className="truncate">{iteration.name}</span>
                  <Badge tone={statusTones[iteration.status]}>
                    {t(`iterations:statuses.${iteration.status}`)}
                  </Badge>
                </CardTitle>
                <p className="text-xs text-muted-foreground">
                  {iteration.startDate} → {iteration.endDate} · {iteration.workItemCount}{" "}
                  {t("iterations:workItemCount")} ·{" "}
                  {t("iterations:hours", {
                    logged: iteration.loggedHours,
                    estimated: iteration.estimatedHours,
                  })}
                </p>
                {iteration.goal ? (
                  <p className="mt-1 text-sm text-muted-foreground">{iteration.goal}</p>
                ) : null}
              </CardHeader>
              <CardContent className="flex gap-2">
                {iteration.status === "planned" ? (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      void startIteration
                        .mutateAsync(iteration.id)
                        .then(() => toast.success(t("iterations:started")))
                        .catch(errorMessage)
                    }
                  >
                    <Play className="h-3.5 w-3.5" />
                    {t("iterations:start")}
                  </Button>
                ) : null}
                {iteration.status === "active" ? (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      void completeIteration
                        .mutateAsync(iteration.id)
                        .then(() => toast.success(t("iterations:completed")))
                        .catch(errorMessage)
                    }
                  >
                    <Square className="h-3.5 w-3.5" />
                    {t("iterations:complete")}
                  </Button>
                ) : null}
                <Button
                  size="sm"
                  variant="ghost"
                  aria-label={t("iterations:edit")}
                  onClick={() => openEdit(iteration)}
                >
                  <Pencil className="h-3.5 w-3.5" />
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  aria-label={t("common:delete")}
                  onClick={() => void handleDelete(iteration.id)}
                >
                  <Trash2 className="h-3.5 w-3.5 text-destructive" />
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {activeIteration === undefined ? (
        <EmptyState>{t("iterations:noActive")}</EmptyState>
      ) : burndown.isPending ? (
        <LoadingState />
      ) : burndown.isError || burndown.data === undefined ? (
        <ErrorState onRetry={() => void burndown.refetch()} />
      ) : (
        <BurndownChart burndown={burndown.data} />
      )}

      <Dialog
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) {
            resetForm();
          }
        }}
        title={editingId === null ? t("iterations:createTitle") : t("iterations:editTitle")}
        closeLabel={t("common:cancel")}
      >
        <div className="space-y-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("iterations:name")}</span>
            <Input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={t("iterations:namePlaceholder")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("iterations:goal")}</span>
            <Textarea
              value={goal}
              onChange={(event) => setGoal(event.target.value)}
              placeholder={t("iterations:goalPlaceholder")}
            />
          </label>
          <div className="grid grid-cols-2 gap-4">
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("iterations:startDate")}</span>
              <Input
                type="date"
                value={startDate}
                onChange={(event) => setStartDate(event.target.value)}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("iterations:endDate")}</span>
              <Input
                type="date"
                value={endDate}
                onChange={(event) => setEndDate(event.target.value)}
              />
            </label>
          </div>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("iterations:milestone")}</span>
            <Select
              value={milestoneId}
              onValueChange={setMilestoneId}
              options={[
                { value: "", label: t("iterations:milestoneNone") },
                ...(milestones.data ?? []).map((milestone) => ({
                  value: milestone.id,
                  label: `${milestone.name}（${milestone.date}）`,
                })),
              ]}
              ariaLabel={t("iterations:milestone")}
            />
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleSubmit()}
              disabled={
                createIteration.isPending ||
                updateIteration.isPending ||
                name.trim() === "" ||
                startDate === "" ||
                endDate === ""
              }
            >
              {editingId === null ? t("common:create") : t("common:save")}
            </Button>
          </div>
        </div>
      </Dialog>
    </div>
  );
}
