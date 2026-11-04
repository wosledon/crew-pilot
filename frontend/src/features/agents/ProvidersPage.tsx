import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Pencil, Plug, Plus, RefreshCw, Star, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/Card";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import {
  useCreateProvider,
  useDeleteProvider,
  useProviders,
  useSetDefaultProvider,
  useSyncProviderModels,
  useTestProvider,
  useUpdateProvider,
} from "./api";
import { ProviderModels } from "./ProviderModels";

/** 模型供应商管理：自定义端点 + models.dev 同步 + 连通性测试 */ export function ProvidersPage() {
  const { t } = useTranslation(["providers", "common"]);
  const providers = useProviders();
  const createProvider = useCreateProvider();
  const updateProvider = useUpdateProvider();
  const deleteProvider = useDeleteProvider();
  const testProvider = useTestProvider();
  const syncModels = useSyncProviderModels();
  const setDefault = useSetDefaultProvider();
  const confirm = useConfirm();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState("");
  const [baseUrl, setBaseUrl] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editEnabled, setEditEnabled] = useState(true);
  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");
  const handleCreate = async () => {
    try {
      if (editingId === null) {
        await createProvider.mutateAsync({
          name: name.trim(),
          baseUrl: baseUrl.trim(),
          apiKey,
        });
        toast.success(t("providers:created"));
      } else {
        await updateProvider.mutateAsync({
          id: editingId,
          name: name.trim(),
          baseUrl: baseUrl.trim(),
          enabled: editEnabled,
          apiKey: apiKey.trim() || undefined,
        });
        toast.success(t("providers:updated"));
      }
      setDialogOpen(false);
      setEditingId(null);
      setName("");
      setBaseUrl("");
      setApiKey("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };
  const handleTest = async (id: string) => {
    try {
      const result = await testProvider.mutateAsync(id);
      if (result.ok) {
        toast.success(t("providers:testOk", { count: result.modelCount }));
      } else {
        toast.error(t("providers:testFail", { message: result.message }));
      }
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };
  const handleSync = async (id: string) => {
    try {
      const result = await syncModels.mutateAsync({ id });
      toast.success(t("providers:synced", { count: result.synced }));
    } catch (error) {
      toast.error(t("providers:syncFailed", { message: errorMessage(error) }));
    }
  };
  const handleDelete = async (id: string) => {
    const approved = await confirm({
      title: t("providers:deleteConfirm.title"),
      description: t("providers:deleteConfirm.description"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteProvider.mutateAsync(id);
      toast.success(t("providers:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };
  const handleSetDefault = async (id: string) => {
    try {
      await setDefault.mutateAsync(id);
      toast.success(t("providers:defaultSet"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };
  if (providers.isPending) {
    return <LoadingState />;
  }
  if (providers.isError) {
    return <ErrorState onRetry={() => void providers.refetch()} />;
  }
  const items = providers.data ?? [];
  return (
    <div className="space-y-6">
      {" "}
      <div className="flex items-center justify-between">
        {" "}
        <div>
          {" "}
          <h1 className="text-xl font-semibold">{t("providers:title")}</h1>{" "}
          <p className="text-sm text-muted-foreground">{t("providers:description")}</p>{" "}
        </div>{" "}
        <Button
          onClick={() => {
            setEditingId(null);
            setName("");
            setBaseUrl("");
            setApiKey("");
            setDialogOpen(true);
          }}
        >
          {" "}
          <Plus className="h-4 w-4" /> {t("providers:create")}{" "}
        </Button>{" "}
      </div>{" "}
      {items.length === 0 ? (
        <EmptyState>{t("providers:empty")}</EmptyState>
      ) : (
        <div className="grid gap-4 grid-cols-[repeat(auto-fill,minmax(320px,1fr))]">
          {" "}
          {items.map((provider) => (
            <Card key={provider.id}>
              {" "}
              <CardHeader>
                {" "}
                <CardTitle className="flex items-center justify-between gap-2">
                  {" "}
                  <span className="truncate">{provider.name}</span>{" "}
                  <div className="flex shrink-0 items-center gap-1.5">
                    {" "}
                    {provider.isDefault ? (
                      <Badge tone="primary">{t("providers:default")}</Badge>
                    ) : null}{" "}
                    <Badge tone={provider.enabled ? "success" : "neutral"}>
                      {" "}
                      {provider.enabled ? t("providers:enabled") : t("providers:disabled")}{" "}
                    </Badge>{" "}
                  </div>{" "}
                </CardTitle>{" "}
                <CardDescription className="truncate">{provider.baseUrl}</CardDescription>{" "}
              </CardHeader>{" "}
              <CardContent className="space-y-3">
                {" "}
                <p className="text-xs text-muted-foreground">
                  {" "}
                  {t("providers:modelCount", { count: provider.modelCount })}{" "}
                </p>{" "}
                {expandedId === provider.id ? <ProviderModels providerId={provider.id} /> : null}{" "}
                <div className="flex flex-wrap gap-2">
                  {" "}
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={testProvider.isPending}
                    onClick={() => void handleTest(provider.id)}
                  >
                    {" "}
                    <Plug className="h-3.5 w-3.5" />{" "}
                    {testProvider.isPending ? t("providers:testing") : t("providers:test")}{" "}
                  </Button>{" "}
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={syncModels.isPending}
                    onClick={() => void handleSync(provider.id)}
                  >
                    {" "}
                    <RefreshCw className="h-3.5 w-3.5" />{" "}
                    {syncModels.isPending ? t("providers:syncing") : t("providers:sync")}{" "}
                  </Button>{" "}
                  {!provider.isDefault ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={!provider.enabled || setDefault.isPending}
                      onClick={() => void handleSetDefault(provider.id)}
                    >
                      {" "}
                      <Star className="h-3.5 w-3.5" /> {t("providers:setDefault")}{" "}
                    </Button>
                  ) : null}{" "}
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setEditingId(provider.id);
                      setName(provider.name);
                      setBaseUrl(provider.baseUrl);
                      setApiKey("");
                      setEditEnabled(provider.enabled);
                      setDialogOpen(true);
                    }}
                  >
                    <Pencil className="h-3.5 w-3.5" />
                    {t("providers:edit")}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => setExpandedId(expandedId === provider.id ? null : provider.id)}
                  >
                    {" "}
                    {t("providers:models")}{" "}
                  </Button>{" "}
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDelete(provider.id)}
                  >
                    {" "}
                    <Trash2 className="h-3.5 w-3.5 text-destructive" />{" "}
                  </Button>{" "}
                </div>{" "}
              </CardContent>{" "}
            </Card>
          ))}{" "}
        </div>
      )}{" "}
      <Dialog
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) {
            setEditingId(null);
          }
        }}
        title={editingId === null ? t("providers:createTitle") : t("providers:editTitle")}
        closeLabel={t("common:cancel")}
      >
        {" "}
        <div className="space-y-4">
          {" "}
          <label className="block space-y-1.5 text-sm">
            {" "}
            <span className="font-medium">{t("providers:name")}</span>{" "}
            <Input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={t("providers:namePlaceholder")}
            />{" "}
          </label>{" "}
          <label className="block space-y-1.5 text-sm">
            {" "}
            <span className="font-medium">{t("providers:baseUrl")}</span>{" "}
            <Input
              value={baseUrl}
              onChange={(event) => setBaseUrl(event.target.value)}
              placeholder={t("providers:baseUrlPlaceholder")}
            />{" "}
          </label>{" "}
          <label className="block space-y-1.5 text-sm">
            {" "}
            <span className="font-medium">{t("providers:apiKey")}</span>{" "}
            <Input
              type="password"
              value={apiKey}
              onChange={(event) => setApiKey(event.target.value)}
              placeholder={
                editingId === null
                  ? t("providers:apiKeyPlaceholder")
                  : t("providers:apiKeyRotatePlaceholder")
              }
            />{" "}
            <span className="text-xs text-muted-foreground">
              {editingId === null ? t("providers:apiKeyHint") : t("providers:apiKeyRotateHint")}
            </span>{" "}
          </label>{" "}
          {editingId !== null ? (
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("providers:enabledLabel")}</span>
              <Select
                value={editEnabled ? "true" : "false"}
                onValueChange={(value) => setEditEnabled(value === "true")}
                options={[
                  { value: "true", label: t("providers:enabled") },
                  { value: "false", label: t("providers:disabled") },
                ]}
                ariaLabel={t("providers:enabledLabel")}
              />
            </label>
          ) : null}
          <div className="flex justify-end gap-2">
            {" "}
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              {" "}
              {t("common:cancel")}{" "}
            </Button>{" "}
            <Button
              onClick={() => void handleCreate()}
              disabled={
                createProvider.isPending ||
                updateProvider.isPending ||
                name.trim() === "" ||
                baseUrl.trim() === "" ||
                (editingId === null && apiKey.trim() === "")
              }
            >
              {" "}
              {editingId === null ? t("common:create") : t("common:save")}{" "}
            </Button>{" "}
          </div>{" "}
        </div>{" "}
      </Dialog>{" "}
    </div>
  );
}
