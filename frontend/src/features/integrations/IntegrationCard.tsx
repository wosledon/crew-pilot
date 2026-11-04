import { useState } from "react";
import { useTranslation } from "react-i18next";
import { PlugZap, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { ApiError } from "@/lib/types";
import { formatDateTime } from "@/lib/format";
import {
  useDeleteIntegration,
  useProjectIntegration,
  useTestIntegration,
  useUpsertIntegration,
} from "./api";

export interface IntegrationCardProps {
  projectId: string;
}

/** 项目 CI/CD 集成配置：Token 加密保存，只在服务端用于 gh/glab 调用。 */
export function IntegrationCard({ projectId }: IntegrationCardProps) {
  const { t } = useTranslation(["integrations", "common"]);
  const integration = useProjectIntegration(projectId);
  const upsert = useUpsertIntegration(projectId);
  const remove = useDeleteIntegration(projectId);
  const test = useTestIntegration(projectId);
  const confirm = useConfirm();

  const [platform, setPlatform] = useState("github");
  const [token, setToken] = useState("");
  const [repository, setRepository] = useState("");
  const [testMessage, setTestMessage] = useState<string | null>(null);

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const configured =
    integration.data !== undefined &&
    !(integration.error instanceof ApiError && integration.error.status === 404);

  const handleSave = async () => {
    try {
      await upsert.mutateAsync({
        platform,
        token: token.trim() || undefined,
        repository: repository.trim() || undefined,
      });
      toast.success(t("integrations:saved"));
      setToken("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleTest = async () => {
    try {
      const result = await test.mutateAsync();
      setTestMessage(result.message);
      if (result.ok) {
        toast.success(t("integrations:testOk"));
      } else {
        toast.error(t("integrations:testFailed"));
      }
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async () => {
    const approved = await confirm({
      title: t("integrations:delete"),
      description: t("integrations:deleteConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await remove.mutateAsync();
      toast.success(t("integrations:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <PlugZap className="h-4 w-4" />
          {t("integrations:title")}
        </CardTitle>
        <p className="text-xs text-muted-foreground">{t("integrations:description")}</p>
      </CardHeader>
      <CardContent className="space-y-4">
        {integration.isPending ? (
          <LoadingState />
        ) : (
          <>
            {configured ? (
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <Badge tone="info">
                  {t(`integrations:platforms.${integration.data!.platform}`)}
                </Badge>
                <Badge tone="success">{t("integrations:hasToken")}</Badge>
                {integration.data!.repository ? (
                  <span className="text-muted-foreground">{integration.data!.repository}</span>
                ) : null}
                <span className="text-xs text-muted-foreground">
                  {formatDateTime(integration.data!.updatedAt)}
                </span>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">{t("integrations:notConfigured")}</p>
            )}

            {configured ? (
              <div className="rounded-md border border-border bg-muted/40 p-2 text-xs">
                <p className="font-medium">{t("integrations:webhook")}</p>
                <code className="block break-all">
                  POST /api/projects/{projectId}/integrations/webhook
                </code>
                <p className="mt-1 text-muted-foreground">{t("integrations:webhookHint")}</p>
              </div>
            ) : null}

            <div className="grid gap-3 sm:grid-cols-2">
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("integrations:platform")}</span>
                <Select
                  value={platform}
                  onValueChange={setPlatform}
                  options={[
                    { value: "github", label: t("integrations:platforms.github") },
                    { value: "gitlab", label: t("integrations:platforms.gitlab") },
                  ]}
                  ariaLabel={t("integrations:platform")}
                />
              </label>
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("integrations:repository")}</span>
                <Input
                  value={repository}
                  onChange={(event) => setRepository(event.target.value)}
                  placeholder={t("integrations:repositoryPlaceholder")}
                />
              </label>
            </div>

            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("integrations:token")}</span>
              <Input
                type="password"
                value={token}
                onChange={(event) => setToken(event.target.value)}
                placeholder={
                  configured ? t("integrations:tokenKeep") : t("integrations:tokenPlaceholder")
                }
              />
            </label>

            <div className="flex flex-wrap items-center gap-2">
              <Button
                onClick={() => void handleSave()}
                disabled={upsert.isPending || (!configured && token.trim() === "")}
              >
                {t("integrations:save")}
              </Button>
              <Button
                variant="outline"
                onClick={() => void handleTest()}
                disabled={!configured || test.isPending}
              >
                {t("integrations:test")}
              </Button>
              {configured ? (
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={t("integrations:delete")}
                  onClick={() => void handleDelete()}
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              ) : null}
              {testMessage ? (
                <span className="text-xs text-muted-foreground">{testMessage}</span>
              ) : null}
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
