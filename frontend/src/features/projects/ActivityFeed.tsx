import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Select } from "@/components/ui/Select";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { formatDateTime } from "@/lib/format";
import { useActivities } from "./api";

export interface ActivityFeedProps {
  projectId: string;
  workItemId?: string;
}

/** 项目活动流：状态变更、评论、附件、AgentRun、里程碑等事件的统一时间线，可按事件类型过滤。 */
export function ActivityFeed({ projectId, workItemId }: ActivityFeedProps) {
  const { t } = useTranslation(["collaboration", "common"]);
  const [eventType, setEventType] = useState("");
  const all = useActivities(projectId, workItemId);
  const activities = useActivities(projectId, workItemId, eventType === "" ? undefined : eventType);

  const eventTypes = [...new Set((all.data ?? []).map((event) => event.eventType))].sort((a, b) =>
    a.localeCompare(b),
  );

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between gap-2">
        <CardTitle>{t("collaboration:activity")}</CardTitle>
        <Select
          value={eventType}
          onValueChange={setEventType}
          options={[
            { value: "", label: t("collaboration:allEvents") },
            ...eventTypes.map((value) => ({
              value,
              label: t(`collaboration:events.${value}`, { defaultValue: value }),
            })),
          ]}
          ariaLabel={t("collaboration:filterEventType")}
          className="w-48"
        />
      </CardHeader>
      <CardContent>
        {activities.isPending ? (
          <LoadingState />
        ) : activities.isError ? (
          <ErrorState onRetry={() => void activities.refetch()} />
        ) : (activities.data ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("collaboration:noActivity")}</p>
        ) : (
          <ul className="space-y-2">
            {(activities.data ?? []).map((event) => (
              <li key={event.id} className="flex items-start justify-between gap-3 text-sm">
                <div className="flex min-w-0 items-center gap-2">
                  <Badge tone={event.actorKind === "agent" ? "info" : "neutral"}>
                    {t(`collaboration:authorKinds.${event.actorKind ?? "user"}`)}
                  </Badge>
                  <span className="truncate">
                    {t(`collaboration:events.${event.eventType}`, {
                      defaultValue: event.eventType,
                    })}
                  </span>
                </div>
                <span className="shrink-0 text-xs text-muted-foreground">
                  {formatDateTime(event.createdAt)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
