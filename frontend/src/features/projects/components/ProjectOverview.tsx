import { useTranslation } from "react-i18next";
import { Card, CardContent } from "@/components/ui/Card";
import { useHoursReport, useIterations, useWorkItems } from "../api";

export interface ProjectOverviewProps {
  projectId: string;
}

/** 项目概览：工作项数量、工时总量、进行中迭代进度。 */
export function ProjectOverview({ projectId }: ProjectOverviewProps) {
  const { t } = useTranslation(["projects", "common", "report"]);
  const workItems = useWorkItems(projectId);
  const iterations = useIterations(projectId);
  const report = useHoursReport(projectId);

  const items = workItems.data ?? [];
  const activeIteration = (iterations.data ?? []).find(
    (iteration) => iteration.status === "active",
  );
  const totalEstimated = report.data?.totalEstimatedHours ?? 0;
  const totalLogged = report.data?.totalLoggedHours ?? 0;
  const progress =
    activeIteration === undefined || activeIteration.estimatedHours <= 0
      ? 0
      : Math.min(100, (activeIteration.loggedHours / activeIteration.estimatedHours) * 100);

  return (
    <div className="grid gap-3 grid-cols-[repeat(auto-fit,minmax(220px,1fr))]">
      <Card>
        <CardContent className="p-4">
          <p className="text-xs text-muted-foreground">{t("projects:workItemCount")}</p>
          <p className="mt-1 text-2xl font-normal">{items.length}</p>
        </CardContent>
      </Card>
      <Card>
        <CardContent className="p-4">
          <p className="text-xs text-muted-foreground">
            {t("report:totalEstimated")} / {t("report:totalLogged")}
          </p>
          <p className="mt-1 text-2xl font-normal">
            {totalEstimated}h / {totalLogged}h
          </p>
        </CardContent>
      </Card>
      <Card>
        <CardContent className="p-4">
          <p className="text-xs text-muted-foreground">{t("projects:activeIteration")}</p>
          {activeIteration === undefined ? (
            <p className="mt-1 text-sm text-muted-foreground">{t("projects:noActiveIteration")}</p>
          ) : (
            <>
              <div className="mt-1 flex items-baseline justify-between gap-2">
                <p className="truncate text-sm font-medium">{activeIteration.name}</p>
                <span className="text-xs text-muted-foreground">
                  {activeIteration.loggedHours}/{activeIteration.estimatedHours}h
                </span>
              </div>
              <div className="mt-2 h-1.5 rounded-full bg-muted">
                <div className="h-1.5 rounded-full bg-primary" style={{ width: `${progress}%` }} />
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
