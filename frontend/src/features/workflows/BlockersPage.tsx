import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Input";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { formatDateTime } from "@/lib/format";
import type { BlockerKind, BlockerResolution } from "@/lib/types";
import { useBlockers, useResolveBlocker } from "./api";

const kindTones: Record<BlockerKind, BadgeTone> = {
  agentblocked: "warning",
  approval: "info",
  failure: "danger",
};

export function BlockersPage() {
  const { projectId = "" } = useParams();
  const { t } = useTranslation(["workflows", "common"]);
  const [statusFilter, setStatusFilter] = useState("open");
  const blockers = useBlockers(projectId, statusFilter);
  const resolveBlocker = useResolveBlocker(projectId);
  const [resolutionDraft, setResolutionDraft] = useState<Record<string, BlockerResolution>>({});
  const [noteDraft, setNoteDraft] = useState<Record<string, string>>({});

  const handleResolve = async (blockerId: string, fallback: BlockerResolution) => {
    const resolution = resolutionDraft[blockerId] ?? fallback;
    try {
      await resolveBlocker.mutateAsync({
        id: blockerId,
        resolution,
        note: noteDraft[blockerId]?.trim() || undefined,
      });
      toast.success(t("workflows:resolved"));
      setNoteDraft((previous) => ({ ...previous, [blockerId]: "" }));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-normal">{t("workflows:blockerQueue")}</h1>
          <p className="text-sm text-muted-foreground">{t("workflows:blockerQueueHint")}</p>
        </div>
        <Select
          value={statusFilter}
          onValueChange={setStatusFilter}
          options={[
            { value: "open", label: t("workflows:blockerStatuses.open") },
            { value: "resolved", label: t("workflows:blockerStatuses.resolved") },
            { value: "", label: t("common:all") },
          ]}
          ariaLabel={t("workflows:blockerStatuses.open")}
          className="w-40"
        />
      </div>

      {blockers.isPending ? (
        <LoadingState />
      ) : blockers.isError ? (
        <ErrorState onRetry={() => void blockers.refetch()} />
      ) : (blockers.data ?? []).length === 0 ? (
        <EmptyState>{t("workflows:noOpenBlockers")}</EmptyState>
      ) : (
        <div className="space-y-3">
          {(blockers.data ?? []).map((blocker) => (
            <Card key={blocker.id}>
              <CardHeader>
                <CardTitle className="flex flex-wrap items-center gap-2 text-sm">
                  <Badge tone={kindTones[blocker.kind]}>
                    {t(`workflows:blockerKinds.${blocker.kind}`)}
                  </Badge>
                  <Badge tone={blocker.status === "open" ? "warning" : "neutral"}>
                    {t(`workflows:blockerStatuses.${blocker.status}`)}
                  </Badge>
                  <span className="text-muted-foreground">{blocker.workItemTitle}</span>
                  <span className="ml-auto text-xs text-muted-foreground">
                    {formatDateTime(blocker.createdAt)}
                  </span>
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <p className="text-xs font-medium text-muted-foreground">
                    {t("workflows:blockerReason")}
                  </p>
                  <p className="mt-1 whitespace-pre-wrap text-sm">{blocker.reason}</p>
                </div>

                {blocker.contextSnapshot ? (
                  <div>
                    <p className="text-xs font-medium text-muted-foreground">
                      {t("workflows:context")}
                    </p>
                    <pre className="mt-1 max-h-40 overflow-auto whitespace-pre-wrap rounded-md bg-muted/40 p-2 text-xs">
                      {blocker.contextSnapshot}
                    </pre>
                  </div>
                ) : null}

                {blocker.status === "open" ? (
                  <div className="flex flex-wrap items-end gap-2 border-t border-border pt-3">
                    <label className="block space-y-1.5 text-sm">
                      <span className="font-medium">{t("workflows:resolution")}</span>
                      <Select
                        value={resolutionDraft[blocker.id] ?? "continue"}
                        onValueChange={(value) =>
                          setResolutionDraft((previous) => ({
                            ...previous,
                            [blocker.id]: value as BlockerResolution,
                          }))
                        }
                        options={[
                          { value: "continue", label: t("workflows:resolutions.continue") },
                          { value: "redirect", label: t("workflows:resolutions.redirect") },
                          { value: "cancel", label: t("workflows:resolutions.cancel") },
                        ]}
                        ariaLabel={t("workflows:resolution")}
                        className="w-40"
                      />
                    </label>
                    <Textarea
                      value={noteDraft[blocker.id] ?? ""}
                      onChange={(event) =>
                        setNoteDraft((previous) => ({
                          ...previous,
                          [blocker.id]: event.target.value,
                        }))
                      }
                      placeholder={t("workflows:notePlaceholder")}
                      className="min-h-16 flex-1"
                    />
                    <Button
                      onClick={() => void handleResolve(blocker.id, "continue")}
                      disabled={resolveBlocker.isPending}
                    >
                      {t("workflows:resolve")}
                    </Button>
                  </div>
                ) : blocker.resolutionNote ? (
                  <p className="text-xs text-muted-foreground">
                    {t(`workflows:resolutions.${blocker.resolution ?? "continue"}`)}：
                    {blocker.resolutionNote}
                  </p>
                ) : null}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
