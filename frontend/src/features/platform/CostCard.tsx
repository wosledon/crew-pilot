import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState, LoadingState } from "@/components/ui/States";
import type { ReportFilters } from "@/features/projects/api";
import { useCostReport } from "./api";

export interface CostCardProps {
  projectId: string;
  filters?: ReportFilters;
}

/** Token 成本卡：AgentRun token × 模型单价（USD）。 */
export function CostCard({ projectId, filters }: CostCardProps) {
  const { t } = useTranslation(["report", "common"]);
  const cost = useCostReport(projectId, filters);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("report:costTitle")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("report:costDescription")}</p>
      </CardHeader>
      <CardContent>
        {cost.isPending ? (
          <LoadingState />
        ) : cost.isError ? (
          <ErrorState onRetry={() => void cost.refetch()} />
        ) : (cost.data?.byModel ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("report:costEmpty")}</p>
        ) : (
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-3">
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:costTotal")}</p>
                <p className="text-lg font-medium">${cost.data!.totalCost.toFixed(4)}</p>
              </div>
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:costPromptTokens")}</p>
                <p className="text-lg font-medium">{cost.data!.totalPromptTokens}</p>
              </div>
              <div className="rounded-lg border border-border p-3">
                <p className="text-xs text-muted-foreground">{t("report:costCompletionTokens")}</p>
                <p className="text-lg font-medium">{cost.data!.totalCompletionTokens}</p>
              </div>
            </div>
            <div>
              <p className="mb-1.5 text-sm font-medium">{t("report:costByModel")}</p>
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs text-muted-foreground">
                    <th className="pb-1 font-medium">{t("report:group")}</th>
                    <th className="pb-1 text-right font-medium">{t("report:costPromptTokens")}</th>
                    <th className="pb-1 text-right font-medium">
                      {t("report:costCompletionTokens")}
                    </th>
                    <th className="pb-1 text-right font-medium">{t("report:costTotal")}</th>
                  </tr>
                </thead>
                <tbody>
                  {cost.data!.byModel.map((model) => (
                    <tr key={`${model.provider}-${model.model}`} className="border-t border-border">
                      <td className="py-1.5">
                        <span className="flex items-center gap-2">
                          <Badge tone="info">{model.provider}</Badge>
                          {model.model}
                        </span>
                      </td>
                      <td className="py-1.5 text-right">{model.promptTokens}</td>
                      <td className="py-1.5 text-right">{model.completionTokens}</td>
                      <td className="py-1.5 text-right">${model.cost.toFixed(6)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
