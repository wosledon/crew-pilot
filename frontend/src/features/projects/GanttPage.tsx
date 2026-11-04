import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Plus, Trash2 } from "lucide-react";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import {
  useCreateDependency,
  useCreateMilestone,
  useCriticalPath,
  useDeleteDependency,
  useDeleteMilestone,
  useDependencies,
  useHoursReport,
  useIterations,
  useMilestones,
  useWorkItems,
} from "./api";
import { priorityTone, statusTone } from "./statusFlow";
import type { DependencyType, Milestone, WorkItemDependency, WorkItemListItem } from "@/lib/types";
import { CostCard } from "@/features/platform/CostCard";
import { HumanCostCard } from "./components/HumanCostCard";
import { EstimateDeviationCard } from "./components/EstimateDeviationCard";
import { cn } from "@/lib/cn";

/** 甘特图：迭代时间线 + 工作项截止日标记（工时维度），纯 CSS 定位自绘 */
export function GanttPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["gantt", "workitems", "report", "common"]);
  const iterations = useIterations(projectId);
  const workItems = useWorkItems(projectId);
  const [iterationFilter, setIterationFilter] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const reportFilters = {
    iterationId: iterationFilter === "" ? undefined : iterationFilter,
    from: fromDate === "" ? undefined : fromDate,
    to: toDate === "" ? undefined : toDate,
  };
  const report = useHoursReport(projectId, reportFilters);
  const milestones = useMilestones(projectId);
  const dependencies = useDependencies(projectId);
  const criticalPath = useCriticalPath(projectId);
  const createDependency = useCreateDependency(projectId);
  const deleteDependency = useDeleteDependency(projectId);
  const createMilestone = useCreateMilestone(projectId);
  const deleteMilestone = useDeleteMilestone(projectId);
  const confirm = useConfirm();
  const [dependencyOpen, setDependencyOpen] = useState(false);
  const [dependencyFrom, setDependencyFrom] = useState("");
  const [dependencyTo, setDependencyTo] = useState("");
  const [dependencyType, setDependencyType] = useState<DependencyType>("finishtostart");
  const [milestoneOpen, setMilestoneOpen] = useState(false);
  const [milestoneName, setMilestoneName] = useState("");
  const [milestoneDate, setMilestoneDate] = useState("");
  const [milestoneDescription, setMilestoneDescription] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleCreateMilestone = async () => {
    try {
      await createMilestone.mutateAsync({
        name: milestoneName.trim(),
        description: milestoneDescription.trim() || undefined,
        date: milestoneDate,
      });
      toast.success(t("gantt:milestoneCreated"));
      setMilestoneOpen(false);
      setMilestoneName("");
      setMilestoneDate("");
      setMilestoneDescription("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDeleteMilestone = async (milestone: Milestone) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: t("gantt:deleteMilestoneConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteMilestone.mutateAsync(milestone.id);
      toast.success(t("gantt:milestoneDeleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleCreateDependency = async () => {
    if (dependencyFrom === "" || dependencyTo === "") {
      return;
    }
    try {
      await createDependency.mutateAsync({
        fromWorkItemId: dependencyFrom,
        toWorkItemId: dependencyTo,
        type: dependencyType,
      });
      toast.success(t("gantt:dependencyCreated"));
      setDependencyOpen(false);
      setDependencyFrom("");
      setDependencyTo("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDeleteDependency = async (dependency: WorkItemDependency) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: `${dependency.fromTitle} → ${dependency.toTitle}`,
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteDependency.mutateAsync(dependency.id);
      toast.success(t("gantt:dependencyDeleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (iterations.isPending || workItems.isPending) {
    return <LoadingState />;
  }

  if (iterations.isError || workItems.isError) {
    return (
      <ErrorState
        onRetry={() => {
          void iterations.refetch();
          void workItems.refetch();
        }}
      />
    );
  }

  const iterationList = iterations.data ?? [];
  const items = workItems.data ?? [];
  const milestoneList = milestones.data ?? [];
  const datedItems = items.filter((item) => item.dueDate !== null);
  const undatedItems = items.filter((item) => item.dueDate === null);
  const criticalIds = new Set(criticalPath.data?.itemIds ?? []);
  const criticalTitles = (criticalPath.data?.itemIds ?? [])
    .map((id) => items.find((item) => item.id === id)?.title)
    .filter((title): title is string => title !== undefined);

  // 时间范围：迭代起止 + 工作项截止日 + 里程碑日期
  const dates: string[] = [
    ...iterationList.flatMap((iteration) => [iteration.startDate, iteration.endDate]),
    ...datedItems.map((item) => item.dueDate as string),
    ...milestoneList.map((milestone) => milestone.date),
  ];
  const today = new Date().toISOString().slice(0, 10);
  dates.push(today);

  if (dates.length === 0) {
    return <EmptyState>{t("gantt:empty")}</EmptyState>;
  }

  const min = dates.reduce((a, b) => (a < b ? a : b));
  const max = dates.reduce((a, b) => (a > b ? a : b));
  const span = Math.max(1, (Date.parse(max) - Date.parse(min)) / (24 * 60 * 60 * 1000));
  const percent = (date: string) =>
    ((Date.parse(date) - Date.parse(min)) / (span * 24 * 60 * 60 * 1000)) * 100;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">{t("gantt:title")}</h1>
        <p className="text-sm text-muted-foreground">{t("gantt:description")}</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("gantt:iterations")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {iterationList.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("gantt:empty")}</p>
          ) : (
            iterationList.map((iteration) => {
              const left = percent(iteration.startDate);
              const width = Math.max(1, percent(iteration.endDate) - left);
              return (
                <div key={iteration.id} className="flex items-center gap-3">
                  <span className="w-32 shrink-0 truncate text-sm">{iteration.name}</span>
                  <div className="relative h-6 flex-1 rounded-md bg-muted/50">
                    <div
                      className="absolute inset-y-0.5 rounded bg-primary/80"
                      style={{ left: `${left}%`, width: `${width}%` }}
                      title={`${iteration.startDate} → ${iteration.endDate}`}
                    />
                    <span className="absolute right-2 top-1/2 -translate-y-1/2 text-[10px] text-muted-foreground">
                      {t("gantt:loggedOfEstimated", {
                        logged: iteration.loggedHours,
                        estimated: iteration.estimatedHours,
                      })}
                    </span>
                  </div>
                </div>
              );
            })
          )}
          {/* 里程碑标记 */}
          {milestoneList.length > 0 ? (
            <div className="relative h-5">
              {milestoneList.map((milestone) => (
                <span
                  key={milestone.id}
                  className="absolute -translate-x-1/2 text-[11px] text-amber-600"
                  style={{ left: `${percent(milestone.date)}%` }}
                  title={`${milestone.name} · ${milestone.date}`}
                >
                  ◆
                </span>
              ))}
            </div>
          ) : null}
          {/* 今日参考线 */}
          <div className="relative h-4">
            <span
              className="absolute top-0 h-full border-l-2 border-destructive"
              style={{ left: `${percent(today)}%` }}
            />
            <span
              className="absolute -translate-x-1/2 text-[10px] text-destructive"
              style={{ left: `${percent(today)}%` }}
            >
              {t("gantt:today")}
            </span>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>{t("gantt:milestones")}</CardTitle>
          <Button size="sm" variant="outline" onClick={() => setMilestoneOpen(true)}>
            <Plus className="h-3.5 w-3.5" />
            {t("gantt:createMilestone")}
          </Button>
        </CardHeader>
        <CardContent>
          {milestones.isPending ? (
            <LoadingState />
          ) : milestones.isError ? (
            <ErrorState onRetry={() => void milestones.refetch()} />
          ) : milestoneList.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("gantt:noMilestones")}</p>
          ) : (
            <ul className="space-y-2">
              {milestoneList.map((milestone) => (
                <li
                  key={milestone.id}
                  className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2"
                >
                  <div className="min-w-0">
                    <p className="text-sm font-medium">
                      {milestone.name}
                      <span className="ml-2 text-xs text-muted-foreground">{milestone.date}</span>
                    </p>
                    {milestone.description ? (
                      <p className="truncate text-xs text-muted-foreground">
                        {milestone.description}
                      </p>
                    ) : null}
                    <p className="text-xs text-muted-foreground">
                      {t("gantt:iterationCount", { count: milestone.iterationCount })}
                    </p>
                  </div>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDeleteMilestone(milestone)}
                  >
                    <Trash2 className="h-3.5 w-3.5 text-destructive" />
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("gantt:workItems")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {datedItems.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("gantt:empty")}</p>
          ) : (
            datedItems.map((item) => (
              <WorkItemRow
                key={item.id}
                item={item}
                percent={percent(item.dueDate as string)}
                today={today}
                critical={criticalIds.has(item.id)}
              />
            ))
          )}
          {undatedItems.length > 0 ? (
            <p className="pt-2 text-xs text-muted-foreground">
              {t("gantt:undated")}：{undatedItems.length}
            </p>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>{t("gantt:dependencies")}</CardTitle>
          <Button size="sm" variant="outline" onClick={() => setDependencyOpen(true)}>
            <Plus className="h-3.5 w-3.5" />
            {t("gantt:createDependency")}
          </Button>
        </CardHeader>
        <CardContent className="space-y-3">
          {dependencies.isPending ? (
            <LoadingState />
          ) : (dependencies.data ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("gantt:noDependencies")}</p>
          ) : (
            <ul className="space-y-2">
              {(dependencies.data ?? []).map((dependency) => (
                <li
                  key={dependency.id}
                  className="flex items-center justify-between gap-2 rounded-md border border-border px-3 py-2 text-sm"
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <span className="truncate">{dependency.fromTitle}</span>
                    <span className="text-muted-foreground">→</span>
                    <span className="truncate">{dependency.toTitle}</span>
                    <Badge tone="info">
                      {dependency.type === "starttostart"
                        ? t("gantt:dependencyTypes.startToStart")
                        : t("gantt:dependencyTypes.finishToStart")}
                    </Badge>
                  </span>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDeleteDependency(dependency)}
                  >
                    <Trash2 className="h-3.5 w-3.5 text-destructive" />
                  </Button>
                </li>
              ))}
            </ul>
          )}
          {criticalPath.data !== undefined && criticalPath.data.itemIds.length > 0 ? (
            <div className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm">
              <p className="font-medium">
                {t("gantt:criticalPath")} ·{" "}
                {t("gantt:criticalPathHours", { hours: criticalPath.data.totalHours })}
              </p>
              <p className="text-muted-foreground">{criticalTitles.join(" → ")}</p>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("gantt:reportFilters")}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap items-end gap-2">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:iteration")}</span>
            <Select
              value={iterationFilter}
              onValueChange={setIterationFilter}
              options={[
                { value: "", label: t("gantt:allIterations") },
                ...iterationList.map((iteration) => ({
                  value: iteration.id,
                  label: iteration.name,
                })),
              ]}
              ariaLabel={t("gantt:iteration")}
              className="w-44"
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:fromDate")}</span>
            <Input
              type="date"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
              className="w-40"
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:toDate")}</span>
            <Input
              type="date"
              value={toDate}
              onChange={(event) => setToDate(event.target.value)}
              className="w-40"
            />
          </label>
          <Button
            variant="outline"
            onClick={() => {
              setIterationFilter("");
              setFromDate("");
              setToDate("");
            }}
          >
            {t("gantt:clearFilters")}
          </Button>
        </CardContent>
      </Card>

      <HoursReportSection
        isLoading={report.isPending}
        isError={report.isError}
        report={report.data}
        onRetry={() => void report.refetch()}
      />

      <CostCard projectId={projectId} filters={reportFilters} />

      <div className="grid gap-4 lg:grid-cols-2">
        <HumanCostCard projectId={projectId} />
        <EstimateDeviationCard projectId={projectId} />
      </div>

      <Dialog
        open={milestoneOpen}
        onOpenChange={setMilestoneOpen}
        title={t("gantt:createMilestone")}
        closeLabel={t("common:cancel")}
      >
        <div className="space-y-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:milestoneName")}</span>
            <Input
              value={milestoneName}
              onChange={(event) => setMilestoneName(event.target.value)}
              placeholder={t("gantt:milestoneName")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:milestoneDate")}</span>
            <Input
              type="date"
              value={milestoneDate}
              onChange={(event) => setMilestoneDate(event.target.value)}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:milestoneDescription")}</span>
            <Textarea
              value={milestoneDescription}
              onChange={(event) => setMilestoneDescription(event.target.value)}
            />
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setMilestoneOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleCreateMilestone()}
              disabled={
                createMilestone.isPending || milestoneName.trim() === "" || milestoneDate === ""
              }
            >
              {t("common:create")}
            </Button>
          </div>
        </div>
      </Dialog>

      <Dialog
        open={dependencyOpen}
        onOpenChange={setDependencyOpen}
        title={t("gantt:createDependency")}
        closeLabel={t("common:cancel")}
      >
        <div className="space-y-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:dependencyFrom")}</span>
            <Select
              value={dependencyFrom}
              onValueChange={setDependencyFrom}
              options={items.map((item) => ({ value: item.id, label: item.title }))}
              placeholder={t("gantt:dependencyFrom")}
              ariaLabel={t("gantt:dependencyFrom")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:dependencyTo")}</span>
            <Select
              value={dependencyTo}
              onValueChange={setDependencyTo}
              options={items.map((item) => ({ value: item.id, label: item.title }))}
              placeholder={t("gantt:dependencyTo")}
              ariaLabel={t("gantt:dependencyTo")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("gantt:dependencyType")}</span>
            <Select
              value={dependencyType}
              onValueChange={(value) => setDependencyType(value as DependencyType)}
              options={[
                { value: "finishtostart", label: t("gantt:dependencyTypes.finishToStart") },
                { value: "starttostart", label: t("gantt:dependencyTypes.startToStart") },
              ]}
              ariaLabel={t("gantt:dependencyType")}
            />
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDependencyOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleCreateDependency()}
              disabled={
                createDependency.isPending ||
                dependencyFrom === "" ||
                dependencyTo === "" ||
                dependencyFrom === dependencyTo
              }
            >
              {t("common:create")}
            </Button>
          </div>
        </div>
      </Dialog>
    </div>
  );
}

function WorkItemRow({
  item,
  percent,
  today,
  critical,
}: {
  item: WorkItemListItem;
  percent: number;
  today: string;
  critical: boolean;
}) {
  const { t } = useTranslation(["gantt", "workitems"]);
  const overdue =
    item.dueDate !== null &&
    item.dueDate < today &&
    item.status !== "done" &&
    item.status !== "closed";

  return (
    <div className="flex items-center gap-3">
      <span className="w-32 shrink-0 truncate text-sm" title={item.title}>
        {critical ? "★ " : ""}
        {item.title}
      </span>
      <div
        className={cn(
          "relative h-6 flex-1 rounded-md bg-muted/50",
          critical ? "ring-2 ring-destructive" : "",
        )}
      >
        <span
          className="absolute inset-y-0.5 flex items-center rounded px-1.5 text-[10px] text-primary-foreground"
          style={{ left: `${percent}%`, backgroundColor: "var(--primary)" }}
        >
          {item.dueDate}
        </span>
        {overdue ? (
          <Badge tone="danger" className="absolute right-2 top-1/2 -translate-y-1/2">
            {t("gantt:overdue")}
          </Badge>
        ) : null}
      </div>
      <div className="flex w-40 shrink-0 items-center gap-1.5">
        <Badge tone={priorityTone(item.priority)}>
          {t(`workitems:priorities.${item.priority}`)}
        </Badge>
        <Badge tone={statusTone(item.status) as BadgeTone}>
          {t(`workitems:statuses.${item.status.toLowerCase()}`)}
        </Badge>
      </div>
    </div>
  );
}

function HoursReportSection({
  isLoading,
  isError,
  report,
  onRetry,
}: {
  isLoading: boolean;
  isError: boolean;
  report: ReturnType<typeof useHoursReport>["data"];
  onRetry: () => void;
}) {
  const { t } = useTranslation(["report", "common"]);

  if (isLoading) {
    return <LoadingState />;
  }

  if (isError || report === undefined) {
    return <ErrorState onRetry={onRetry} />;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("report:title")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("report:description")}</p>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div className="rounded-lg border border-border p-3">
            <p className="text-xs text-muted-foreground">{t("report:totalEstimated")}</p>
            <p className="text-lg font-semibold">{report.totalEstimatedHours}h</p>
          </div>
          <div className="rounded-lg border border-border p-3">
            <p className="text-xs text-muted-foreground">{t("report:totalLogged")}</p>
            <p className="text-lg font-semibold">{report.totalLoggedHours}h</p>
          </div>
        </div>

        <HoursGroupTable
          title={t("report:byType")}
          rows={report.byType.map((group) => ({
            key: group.key,
            label: t(`report:typeLabels.${group.key}`, { defaultValue: group.label }),
            estimatedHours: group.estimatedHours,
            loggedHours: group.loggedHours,
          }))}
        />
        <HoursGroupTable
          title={t("report:byAssignee")}
          rows={report.byAssignee.map((group) => ({
            key: group.key,
            label: group.key === "unassigned" ? t("report:unassigned") : group.label,
            estimatedHours: group.estimatedHours,
            loggedHours: group.loggedHours,
          }))}
        />
        <HoursGroupTable
          title={t("report:byMember")}
          rows={report.byMember.map((group) => ({
            key: group.key,
            label: group.label,
            estimatedHours: group.estimatedHours,
            loggedHours: group.loggedHours,
          }))}
        />
      </CardContent>
    </Card>
  );
}

function HoursGroupTable({
  title,
  rows,
}: {
  title: string;
  rows: { key: string; label: string; estimatedHours: number; loggedHours: number }[];
}) {
  const { t } = useTranslation("report");

  if (rows.length === 0) {
    return null;
  }

  return (
    <div>
      <p className="mb-1.5 text-sm font-medium">{title}</p>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-xs text-muted-foreground">
            <th className="pb-1 font-medium">{t("report:group")}</th>
            <th className="pb-1 text-right font-medium">{t("report:totalEstimated")}</th>
            <th className="pb-1 text-right font-medium">{t("report:totalLogged")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key} className="border-t border-border">
              <td className="py-1.5">{row.label}</td>
              <td className="py-1.5 text-right">{row.estimatedHours}h</td>
              <td
                className={cn(
                  "py-1.5 text-right",
                  row.loggedHours > row.estimatedHours && "text-destructive",
                )}
              >
                {row.loggedHours}h
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
