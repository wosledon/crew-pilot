import { useTranslation } from "react-i18next";
import { Clock, Bot } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Select";
import { nextStatuses, priorityTone, statusTone } from "../statusFlow";
import type { WorkItemListItem } from "@/lib/types";

export interface WorkItemCardProps {
  item: WorkItemListItem;
  onTransition: (id: string, toStatus: string) => void;
  onLogWork: (item: WorkItemListItem) => void;
  onAssign: (item: WorkItemListItem) => void;
  transitioning: boolean;
}

export function WorkItemCard({
  item,
  onTransition,
  onLogWork,
  onAssign,
  transitioning,
}: WorkItemCardProps) {
  const { t } = useTranslation(["workitems", "common", "agentruns"]);
  const options = nextStatuses(item.type, item.status);

  return (
    <div className="rounded-lg border border-border bg-card p-4 shadow-sm transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone="neutral">{t(`workitems:types.${item.type}`)}</Badge>
            <Badge tone={statusTone(item.status)}>
              {t(`workitems:statuses.${item.status.toLowerCase()}`)}
            </Badge>
            <Badge tone={priorityTone(item.priority)}>
              {t(`workitems:priorities.${item.priority}`)}
            </Badge>
          </div>
          <p className="mt-2 truncate text-sm font-medium" title={item.title}>
            {item.title}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {item.assigneeName ? `@${item.assigneeName}` : "—"} ·{" "}
            {t("workitems:hoursSummary", {
              logged: item.loggedHours,
              estimated: item.estimatedHours ?? "—",
            })}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {options.length > 0 ? (
            <Select
              value=""
              onValueChange={(value) => onTransition(item.id, value)}
              options={options.map((status) => ({
                value: status,
                label: t(`workitems:statuses.${status.toLowerCase()}`),
              }))}
              placeholder={t("workitems:nextStatus")}
              ariaLabel={t("workitems:nextStatus")}
              className="w-32"
            />
          ) : null}
          <Button
            variant="outline"
            size="icon"
            aria-label={t("agentruns:assign")}
            disabled={transitioning}
            onClick={() => onAssign(item)}
          >
            <Bot className="h-4 w-4" />
          </Button>
          <Button
            variant="outline"
            size="icon"
            aria-label={t("workitems:logWork")}
            disabled={transitioning}
            onClick={() => onLogWork(item)}
          >
            <Clock className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
