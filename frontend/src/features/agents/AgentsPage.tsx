import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Bot, Play, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/Card";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { api } from "@/lib/api";
import { useAgent, useAgents, useCreateAgent, useDeleteAgent, useProvider, useProviders } from "./api";

const defaultTools = '["read_file","write_file","list_files","run_command"]';
const defaultWhitelist = '["git","dotnet","npm"]';

const jobKeys = ["product", "architect", "developer", "qa", "manager", "ops"];

/** Agent 数字员工管理：岗位 / 模型 / 系统提示词 / 工具权限 */
export function AgentsPage() {
  const { t } = useTranslation(["agents", "common"]);
  const agents = useAgents();
  const providers = useProviders();
  const createAgent = useCreateAgent();
  const deleteAgent = useDeleteAgent();
  const confirm = useConfirm();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState("");
  const [job, setJob] = useState("developer");
  const [providerId, setProviderId] = useState("");
  const [model, setModel] = useState("");
  const [systemPrompt, setSystemPrompt] = useState("");
  const [tools, setTools] = useState(defaultTools);
  const [commandWhitelist, setCommandWhitelist] = useState(defaultWhitelist);

  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewAgentId, setPreviewAgentId] = useState<string | null>(null);
  const [previewPrompt, setPreviewPrompt] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleCreate = async () => {
    try {
      await createAgent.mutateAsync({
        name: name.trim(),
        job,
        providerId,
        model: model.trim(),
        systemPrompt,
        tools,
        commandWhitelist,
      });
      toast.success(t("agents:created"));
      setDialogOpen(false);
      setName("");
      setSystemPrompt("");
      setModel("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async (id: string) => {
    const approved = await confirm({
      title: t("agents:deleteConfirm.title"),
      description: t("agents:deleteConfirm.description"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteAgent.mutateAsync(id);
      toast.success(t("agents:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (agents.isPending || providers.isPending) {
    return <LoadingState />;
  }

  if (agents.isError || providers.isError) {
    return (
      <ErrorState
        onRetry={() => {
          void agents.refetch();
          void providers.refetch();
        }}
      />
    );
  }

  const items = agents.data ?? [];
  const providerList = providers.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t("agents:title")}</h1>
          <p className="text-sm text-muted-foreground">{t("agents:description")}</p>
        </div>
        <Button
          onClick={() => {
            setProviderId(providerList[0]?.id ?? "");
            setDialogOpen(true);
          }}
          disabled={providerList.length === 0}
        >
          <Plus className="h-4 w-4" />
          {t("agents:create")}
        </Button>
      </div>

      {providerList.length === 0 ? (
        <EmptyState>{t("agents:noProviders")}</EmptyState>
      ) : items.length === 0 ? (
        <EmptyState>{t("agents:empty")}</EmptyState>
      ) : (
        <div className="grid gap-4 grid-cols-[repeat(auto-fill,minmax(320px,1fr))]">
          {items.map((agent) => (
            <Card key={agent.id}>
              <CardHeader>
                <CardTitle className="flex items-center justify-between gap-2">
                  <span className="flex min-w-0 items-center gap-2">
                    <Bot className="h-4 w-4 shrink-0 text-primary" />
                    <span className="truncate">{agent.name}</span>
                  </span>
                  <Badge tone={agent.enabled ? "success" : "neutral"}>
                    {t(`agents:jobs.${agent.job}`)}
                  </Badge>
                </CardTitle>
                <CardDescription className="truncate">
                  {agent.providerName} · {agent.model}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <p className="text-xs text-muted-foreground">
                  {t("agents:runCount", { count: agent.runCount })}
                </p>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setPreviewAgentId(agent.id);
                      setPreviewPrompt("");
                      setPreviewOpen(true);
                    }}
                  >
                    <Play className="h-3.5 w-3.5" />
                    {t("agents:preview")}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDelete(agent.id)}
                  >
                    <Trash2 className="h-3.5 w-3.5 text-destructive" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        title={t("agents:createTitle")}
        closeLabel={t("common:cancel")}
        className="max-w-2xl"
      >
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("agents:name")}</span>
              <Input
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder={t("agents:namePlaceholder")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("agents:job")}</span>
              <Select
                value={job}
                onValueChange={setJob}
                options={jobKeys.map((key) => ({
                  value: key,
                  label: t(`agents:jobs.${key}`),
                }))}
                ariaLabel={t("agents:job")}
              />
            </label>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("agents:provider")}</span>
              <Select
                value={providerId}
                onValueChange={(value) => {
                  setProviderId(value);
                  setModel("");
                }}
                options={providerList.map((provider) => ({
                  value: provider.id,
                  label: provider.name,
                }))}
                placeholder={t("agents:selectProvider")}
                ariaLabel={t("agents:provider")}
              />
            </label>
            <ModelSelect
              providerId={providerId}
              value={model}
              onChange={setModel}
            />
          </div>

          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("agents:systemPrompt")}</span>
            <Textarea
              value={systemPrompt}
              onChange={(event) => setSystemPrompt(event.target.value)}
              placeholder={t("agents:systemPromptPlaceholder")}
              className="min-h-24"
            />
          </label>

          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("agents:tools")}</span>
            <Input value={tools} onChange={(event) => setTools(event.target.value)} />
            <span className="text-xs text-muted-foreground">{t("agents:toolsHint")}</span>
          </label>

          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("agents:commandWhitelist")}</span>
            <Input
              value={commandWhitelist}
              onChange={(event) => setCommandWhitelist(event.target.value)}
            />
            <span className="text-xs text-muted-foreground">
              {t("agents:commandWhitelistHint")}
            </span>
          </label>

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleCreate()}
              disabled={
                createAgent.isPending ||
                name.trim() === "" ||
                providerId === "" ||
                model.trim() === "" ||
                systemPrompt.trim() === ""
              }
            >
              {t("common:create")}
            </Button>
          </div>
        </div>
      </Dialog>

      {previewAgentId !== null ? (
        <PreviewDialog
          open={previewOpen}
          onOpenChange={(open) => {
            setPreviewOpen(open);
            if (!open) {
              setPreviewAgentId(null);
            }
          }}
          agentId={previewAgentId}
          prompt={previewPrompt}
          onPromptChange={setPreviewPrompt}
        />
      ) : null}
    </div>
  );
}

function ModelSelect({
  providerId,
  value,
  onChange,
}: {
  providerId: string;
  value: string;
  onChange: (value: string) => void;
}) {
  const { t } = useTranslation("agents");
  const provider = useProvider(providerId === "" ? undefined : providerId);
  const models = provider.data?.models ?? [];

  return (
    <label className="block space-y-1.5 text-sm">
      <span className="font-medium">{t("agents:model")}</span>
      {models.length > 0 ? (
        <Select
          value={value}
          onValueChange={onChange}
          options={models.map((model) => ({
            value: model.modelId,
            label: model.displayName || model.modelId,
          }))}
          placeholder={t("agents:model")}
          ariaLabel={t("agents:model")}
        />
      ) : (
        <Input
          value={value}
          onChange={(event) => onChange(event.target.value)}
          placeholder={t("agents:modelPlaceholder")}
        />
      )}
    </label>
  );
}

function PreviewDialog({
  open,
  onOpenChange,
  agentId,
  prompt,
  onPromptChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  agentId: string;
  prompt: string;
  onPromptChange: (value: string) => void;
}) {
  const { t } = useTranslation(["agents", "agentruns", "common"]);
  const agent = useAgent(agentId);
  const [runId, setRunId] = useState<string | null>(null);

  const handleRun = async () => {
    try {
      const run = await api.post<{ id: string }>(`/agents/${agentId}/runs`, {
        prompt,
      });
      setRunId(run.id);
      toast.success(t("agents:previewStarted"));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={`${t("agents:previewTitle")} · ${agent.data?.name ?? ""}`}
      closeLabel={t("common:cancel")}
    >
      <div className="space-y-4">
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("agents:previewPrompt")}</span>
          <Textarea
            value={prompt}
            onChange={(event) => onPromptChange(event.target.value)}
            className="min-h-24"
          />
        </label>
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t("common:cancel")}
          </Button>
          <Button onClick={() => void handleRun()} disabled={prompt.trim() === ""}>
            <Play className="h-4 w-4" />
            {t("agents:previewRun")}
          </Button>
        </div>
        {runId !== null ? (
          <p className="text-sm text-muted-foreground">
            {t("agentruns:title")} #{runId.slice(0, 8)} · {t("agentruns:statuses.queued")}
          </p>
        ) : null}
      </div>
    </Dialog>
  );
}
