import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { useEstimateDeviation } from "../api";

export interface EstimateDeviationCardProps {
  projectId: string;
}

/** 估算偏差：按类型对比预估 / 已耗，标记超支项。 */
export function EstimateDeviationCard({ projectId }: EstimateDeviationCardProps) {
  const { t } = useTranslation(["report", "common"]);
  const report = useEstimateDeviation(projectId);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("report:deviationTitle")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("report:deviationDescription")}</p>
      </CardHeader>
      <CardContent>
        {report.isPending ? (
          <LoadingState />
        ) : report.isError ? (
          <ErrorState onRetry={() => void report.refetch()} />
        ) : (report.data?.byType ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("report:deviationEmpty")}</p>
        ) : (
          <div className="space-y-4">
            <div className="grid grid-cols-4 gap-3">
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:estimated")}</p>
                <p className="text-lg font-medium">{report.data!.estimatedHours}h</p>
              </div>
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:logged")}</p>
                <p className="text-lg font-medium">{report.data!.loggedHours}h</p>
              </div>
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:deviation")}</p>
                <p className="text-lg font-medium">
                  {report.data!.deviationHours > 0 ? "+" : ""}
                  {report.data!.deviationHours}h
                </p>
              </div>
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:overruns")}</p>
                <p className="text-lg font-medium">
                  {report.data!.overrunCount} / {report.data!.itemCount}
                </p>
              </div>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-muted-foreground">
                  <th className="pb-1 font-medium">{t("report:byType")}</th>
                  <th className="pb-1 text-right font-medium">{t("report:estimated")}</th>
                  <th className="pb-1 text-right font-medium">{t("report:logged")}</th>
                  <th className="pb-1 text-right font-medium">{t("report:deviation")}</th>
                  <th className="pb-1 text-right font-medium">{t("report:overruns")}</th>
                </tr>
              </thead>
              <tbody>
                {report.data!.byType.map((row) => (
                  <tr key={row.key} className="border-t border-border">
                    <td className="py-1.5">
                      {t(`report:typeLabels.${row.key}`, { defaultValue: row.label })}
                    </td>
                    <td className="py-1.5 text-right">{row.estimatedHours}h</td>
                    <td className="py-1.5 text-right">{row.loggedHours}h</td>
                    <td className="py-1.5 text-right">
                      <span
                        className={row.deviationHours > 0 ? "text-destructive" : "text-success"}
                      >
                        {row.deviationHours > 0 ? "+" : ""}
                        {row.deviationHours}h
                        {row.deviationPercent !== null
                          ? ` (${row.deviationPercent > 0 ? "+" : ""}${row.deviationPercent}%)`
                          : ""}
                      </span>
                    </td>
                    <td className="py-1.5 text-right">
                      {row.overrunCount > 0 ? (
                        <Badge tone="danger">
                          {row.overrunCount} / {row.itemCount}
                        </Badge>
                      ) : (
                        <Badge tone="success">0 / {row.itemCount}</Badge>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
