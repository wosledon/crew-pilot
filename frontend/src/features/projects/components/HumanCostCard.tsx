import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { useHumanCostReport } from "../api";

export interface HumanCostCardProps {
  projectId: string;
}

/** 人力成本：工时 × 成员时薪。 */
export function HumanCostCard({ projectId }: HumanCostCardProps) {
  const { t } = useTranslation(["report", "common"]);
  const report = useHumanCostReport(projectId);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("report:humanCostTitle")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("report:humanCostDescription")}</p>
      </CardHeader>
      <CardContent>
        {report.isPending ? (
          <LoadingState />
        ) : report.isError ? (
          <ErrorState onRetry={() => void report.refetch()} />
        ) : (report.data?.byMember ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("report:humanCostEmpty")}</p>
        ) : (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:humanCostTotal")}</p>
                <p className="text-lg font-semibold">{report.data!.totalCost.toFixed(2)}</p>
              </div>
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:hours")}</p>
                <p className="text-lg font-semibold">{report.data!.totalHours}</p>
              </div>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-muted-foreground">
                  <th className="pb-1 font-medium">{t("report:byMember")}</th>
                  <th className="pb-1 text-right font-medium">{t("report:hours")}</th>
                  <th className="pb-1 text-right font-medium">{t("report:hourlyRate")}</th>
                  <th className="pb-1 text-right font-medium">{t("report:humanCostTotal")}</th>
                </tr>
              </thead>
              <tbody>
                {report.data!.byMember.map((member) => (
                  <tr key={member.key} className="border-t border-border">
                    <td className="py-1.5">{member.label}</td>
                    <td className="py-1.5 text-right">{member.hours}</td>
                    <td className="py-1.5 text-right">
                      {member.hourlyRate ?? t("report:rateMissing")}
                    </td>
                    <td className="py-1.5 text-right">{member.cost.toFixed(2)}</td>
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
