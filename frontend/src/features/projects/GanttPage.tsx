import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { useHoursReport, useIterations, useWorkItems } from "./api";
import { priorityTone, statusTone } from "./statusFlow";
import type { WorkItemListItem } from "@/lib/types";
import { cn } from "@/lib/cn";

/** 甘特图：迭代时间线 + 工作项截止日标记（工时维度），纯 CSS 定位自绘 */
export function GanttPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["gantt", "workitems", "report", "common"]);
  const iterations = useIterations(projectId);
  const workItems = useWorkItems(projectId);
  const report = useHoursReport(projectId);

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
  const datedItems = items.filter((item) => item.dueDate !== null);
  const undatedItems = items.filter((item) => item.dueDate === null);

  // 时间范围：迭代起止 + 工作项截止日
  const dates: string[] = [
    ...iterationList.flatMap((iteration) => [iteration.startDate, iteration.endDate]),
    ...datedItems.map((item) => item.dueDate as string),
  ];
  const today = new Date().toISOString().slice(0, 10);
  dates.push(today);

  if (dates.length === 0) {
    return <EmptyState>{t("gantt:empty")}</EmptyState>;
  }

  const min = dates.reduce((a, b) => (a < b ? a : b));
  const max = dates.reduce((a, b) => (a > b ? a : b));
  const span = Math.max(
    1,
    (Date.parse(max) - Date.parse(min)) / (24 * 60 * 60 * 1000),
  );
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
              const width = Math.max(
                1,
                percent(iteration.endDate) - left,
              );
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
        <CardHeader>
          <CardTitle>{t("gantt:workItems")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {datedItems.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("gantt:empty")}</p>
          ) : (
            datedItems.map((item) => (
              <WorkItemRow key={item.id} item={item} percent={percent(item.dueDate as string)} today={today} />
            ))
          )}
          {undatedItems.length > 0 ? (
            <p className="pt-2 text-xs text-muted-foreground">
              {t("gantt:undated")}：{undatedItems.length}
            </p>
          ) : null}
        </CardContent>
      </Card>

      <HoursReportSection
        isLoading={report.isPending}
        isError={report.isError}
        report={report.data}
        onRetry={() => void report.refetch()}
      />

      <p className="text-xs text-muted-foreground">{t("gantt:milestoneHint")}</p>
    </div>
  );
}

function WorkItemRow({
  item,
  percent,
  today,
}: {
  item: WorkItemListItem;
  percent: number;
  today: string;
}) {
  const { t } = useTranslation(["gantt", "workitems"]);
  const overdue = item.dueDate !== null && item.dueDate < today && item.status !== "done" && item.status !== "closed";

  return (
    <div className="flex items-center gap-3">
      <span className="w-32 shrink-0 truncate text-sm" title={item.title}>
        {item.title}
      </span>
      <div className="relative h-6 flex-1 rounded-md bg-muted/50">
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
          {t(`workitems:statuses.${item.status}`)}
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
            label:
              group.key === "unassigned"
                ? t("report:unassigned")
                : group.label,
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
              <td className={cn("py-1.5 text-right", row.loggedHours > row.estimatedHours && "text-destructive")}>
                {row.loggedHours}h
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
