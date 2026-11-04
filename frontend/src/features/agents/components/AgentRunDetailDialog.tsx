import { useTranslation } from "react-i18next";
import { Bot, Clock, FileText, Timer } from "lucide-react";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Dialog } from "@/components/ui/Dialog";
import { ErrorState, LoadingState } from "@/components/ui/States";
import type { AgentRunStatus } from "@/lib/types";
import { useAgentRun } from "../api";

const statusTones: Record<AgentRunStatus, BadgeTone> = {
  queued: "neutral",
  running: "info",
  succeeded: "success",
  failed: "danger",
  canceled: "warning",
  waitingforhuman: "warning",
};

export interface AgentRunDetailDialogProps {
  runId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** AgentRun 详情：实时刷新状态、执行日志时间线与提示词 / 输出 / 错误。 */
export function AgentRunDetailDialog({ runId, open, onOpenChange }: AgentRunDetailDialogProps) {
  const { t } = useTranslation(["agentruns", "common"]);
  const run = useAgentRun(runId ?? undefined);
  const detail = run.data;
  const isLive = detail?.status === "queued" || detail?.status === "running";

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("agentruns:detailTitle")}
      closeLabel={t("common:cancel")}
      className="max-w-3xl"
    >
      {runId === null ? null : run.isPending ? (
        <LoadingState />
      ) : run.isError || detail === undefined ? (
        <ErrorState onRetry={() => void run.refetch()} />
      ) : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Bot className="h-4 w-4 text-primary" />
            <span className="font-medium">{detail.agentName}</span>
            <Badge tone={statusTones[detail.status]}>
              {t(`agentruns:statuses.${detail.status}`)}
            </Badge>
            <Badge tone="neutral">{t(`agentruns:triggers.${detail.trigger}`)}</Badge>
            {isLive ? (
              <span className="flex items-center gap-1 text-xs text-info">
                <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-info" />
                {t("agentruns:live")}
              </span>
            ) : null}
          </div>

          <div className="grid gap-2 text-xs text-muted-foreground sm:grid-cols-2">
            <span className="flex items-center gap-1">
              <Clock className="h-3.5 w-3.5" />
              {t("agentruns:createdAt")}: {formatTime(detail.createdAt)}
            </span>
            <span className="flex items-center gap-1">
              <Clock className="h-3.5 w-3.5" />
              {t("agentruns:finishedAt")}:{" "}
              {detail.finishedAt === null
                ? t("agentruns:runningNow")
                : formatTime(detail.finishedAt)}
            </span>
            <span className="flex items-center gap-1">
              <Timer className="h-3.5 w-3.5" />
              {t("agentruns:duration")}:{" "}
              {detail.duration === null ? "—" : formatDuration(detail.duration)}
            </span>
            <span className="flex items-center gap-1">
              <FileText className="h-3.5 w-3.5" />
              {t("agentruns:tokens", {
                prompt: detail.promptTokens ?? 0,
                completion: detail.completionTokens ?? 0,
              })}
            </span>
          </div>

          {detail.workItemTitle ? (
            <p className="text-sm">
              <span className="text-muted-foreground">{t("agentruns:workItem")}: </span>
              {detail.workItemTitle}
            </p>
          ) : null}

          {detail.error ? (
            <section>
              <h4 className="text-sm font-medium text-destructive">{t("agentruns:error")}</h4>
              <pre className="mt-1 max-h-32 overflow-auto whitespace-pre-wrap rounded-md bg-muted/60 p-2 text-xs text-destructive">
                {detail.error}
              </pre>
            </section>
          ) : null}

          <section>
            <h4 className="text-sm font-medium">{t("agentruns:logs")}</h4>
            {detail.logs.length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">{t("agentruns:logsEmpty")}</p>
            ) : (
              <ol className="mt-2 max-h-80 space-y-2 overflow-auto rounded-md border border-border p-2">
                {detail.logs.map((log) => (
                  <li key={log.sequence} className="flex gap-2">
                    <span className="mt-0.5 shrink-0 font-mono text-xs text-muted-foreground">
                      #{log.sequence}
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <Badge tone={logKindTone(log.kind)}>{log.kind}</Badge>
                        <span className="text-xs text-muted-foreground">
                          {formatTime(log.createdAt)}
                        </span>
                      </div>
                      <pre className="mt-1 whitespace-pre-wrap break-words text-xs">
                        {log.content}
                      </pre>
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </section>

          <section>
            <h4 className="text-sm font-medium">{t("agentruns:prompt")}</h4>
            <pre className="mt-1 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded-md bg-muted/60 p-2 text-xs">
              {detail.prompt}
            </pre>
          </section>

          {detail.output ? (
            <section>
              <h4 className="text-sm font-medium">{t("agentruns:output")}</h4>
              <pre className="mt-1 max-h-60 overflow-auto whitespace-pre-wrap break-words rounded-md bg-muted/60 p-2 text-xs">
                {detail.output}
              </pre>
            </section>
          ) : null}

          {detail.workingDirectory ? (
            <p className="truncate text-xs text-muted-foreground">
              {t("agentruns:workingDirectory")}: {detail.workingDirectory}
            </p>
          ) : null}
        </div>
      )}
    </Dialog>
  );
}

function formatTime(value: string): string {
  return new Date(value).toLocaleString();
}

function formatDuration(iso: string): string {
  // TimeSpan 序列化为 [-]hh:mm:ss.fffffff
  const match = /^(\d+):(\d{2}):(\d{2})/.exec(iso);
  if (match === null) {
    return iso;
  }

  const [, hours, minutes, seconds] = match;
  return `${hours}h ${minutes}m ${seconds}s`;
}

function logKindTone(kind: string): BadgeTone {
  if (kind === "error") {
    return "danger";
  }

  if (kind === "tool") {
    return "info";
  }

  return "neutral";
}
