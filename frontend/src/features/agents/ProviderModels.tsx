import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import type { ModelDto } from "@/lib/types";
import {
  useCreateProviderModel,
  useDeleteProviderModel,
  useProvider,
  useUpdateProviderModel,
} from "./api";

interface ModelForm {
  id?: string;
  modelId: string;
  displayName: string;
  contextLength: string;
  capabilities: string;
  inputPrice: string;
  outputPrice: string;
  enabled: boolean;
}

const emptyForm: ModelForm = {
  modelId: "",
  displayName: "",
  contextLength: "",
  capabilities: "",
  inputPrice: "",
  outputPrice: "",
  enabled: true,
};

function parseNumber(value: string): number | undefined {
  const trimmed = value.trim();
  if (trimmed === "") {
    return undefined;
  }
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : undefined;
}

export interface ProviderModelsProps {
  providerId: string;
}

/** 供应商模型目录：手动增删改/启停（models.dev 同步的本地覆盖）。 */
export function ProviderModels({ providerId }: ProviderModelsProps) {
  const { t } = useTranslation(["providers", "common"]);
  const provider = useProvider(providerId);
  const createModel = useCreateProviderModel(providerId);
  const updateModel = useUpdateProviderModel();
  const deleteModel = useDeleteProviderModel();
  const confirm = useConfirm();
  const [form, setForm] = useState<ModelForm | null>(null);

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const openEdit = (model: ModelDto) =>
    setForm({
      id: model.id,
      modelId: model.modelId,
      displayName: model.displayName,
      contextLength: model.contextLength?.toString() ?? "",
      capabilities: model.capabilities ?? "",
      inputPrice: model.inputPricePerMillion?.toString() ?? "",
      outputPrice: model.outputPricePerMillion?.toString() ?? "",
      enabled: model.enabled,
    });

  const handleSave = async () => {
    if (form === null) {
      return;
    }
    const payload = {
      modelId: form.modelId.trim(),
      displayName: form.displayName.trim() || undefined,
      contextLength: parseNumber(form.contextLength),
      capabilities: form.capabilities.trim() || undefined,
      inputPricePerMillion: parseNumber(form.inputPrice),
      outputPricePerMillion: parseNumber(form.outputPrice),
    };
    try {
      if (form.id === undefined) {
        await createModel.mutateAsync(payload);
      } else {
        await updateModel.mutateAsync({ id: form.id, enabled: form.enabled, ...payload });
      }
      toast.success(t("providers:modelSaved"));
      setForm(null);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async (model: ModelDto) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: t("providers:deleteModelConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteModel.mutateAsync(model.id);
      toast.success(t("providers:modelDeleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (provider.isPending) {
    return <LoadingState />;
  }

  if (provider.isError || provider.data === undefined) {
    return <ErrorState onRetry={() => void provider.refetch()} />;
  }

  const models = provider.data.models;

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <span className="text-xs text-muted-foreground">
          {t("providers:modelCount", { count: models.length })}
        </span>
        <Button size="sm" variant="outline" onClick={() => setForm({ ...emptyForm })}>
          <Plus className="h-3.5 w-3.5" />
          {t("providers:addModel")}
        </Button>
      </div>

      {models.length === 0 ? (
        <p className="text-xs text-muted-foreground">{t("providers:modelsEmpty")}</p>
      ) : (
        <ul className="max-h-56 space-y-1 overflow-y-auto rounded-md border border-border p-2">
          {models.map((model) => (
            <li key={model.id} className="flex items-center justify-between gap-2 text-xs">
              <div className="min-w-0">
                <p className="truncate font-medium">{model.modelId}</p>
                <p className="truncate text-muted-foreground">
                  {model.displayName}
                  {model.contextLength !== null
                    ? ` · ${t("providers:contextLength", { length: model.contextLength })}`
                    : ""}
                  {model.inputPricePerMillion !== null
                    ? ` · ${t("providers:price", {
                        input: model.inputPricePerMillion,
                        output: model.outputPricePerMillion ?? 0,
                      })}`
                    : ""}
                </p>
              </div>
              <div className="flex shrink-0 items-center gap-1">
                <Badge tone={model.enabled ? "success" : "neutral"}>
                  {model.enabled ? t("providers:enabled") : t("providers:disabled")}
                </Badge>
                <Button
                  size="sm"
                  variant="ghost"
                  aria-label={t("providers:editModel")}
                  onClick={() => openEdit(model)}
                >
                  <Pencil className="h-3.5 w-3.5" />
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  aria-label={t("common:delete")}
                  onClick={() => void handleDelete(model)}
                >
                  <Trash2 className="h-3.5 w-3.5 text-destructive" />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <Dialog
        open={form !== null}
        onOpenChange={(open) => {
          if (!open) {
            setForm(null);
          }
        }}
        title={form?.id === undefined ? t("providers:addModel") : t("providers:editModel")}
        closeLabel={t("common:cancel")}
        className="max-w-lg"
      >
        {form !== null ? (
          <div className="space-y-3">
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("providers:modelId")}</span>
              <Input
                value={form.modelId}
                onChange={(event) => setForm({ ...form, modelId: event.target.value })}
                placeholder={t("providers:modelIdPlaceholder")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("providers:displayName")}</span>
              <Input
                value={form.displayName}
                onChange={(event) => setForm({ ...form, displayName: event.target.value })}
                placeholder={t("providers:displayNamePlaceholder")}
              />
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("providers:inputPrice")}</span>
                <Input
                  value={form.inputPrice}
                  onChange={(event) => setForm({ ...form, inputPrice: event.target.value })}
                  placeholder="0.14"
                />
              </label>
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("providers:outputPrice")}</span>
                <Input
                  value={form.outputPrice}
                  onChange={(event) => setForm({ ...form, outputPrice: event.target.value })}
                  placeholder="0.28"
                />
              </label>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("providers:contextLengthLabel")}</span>
                <Input
                  value={form.contextLength}
                  onChange={(event) => setForm({ ...form, contextLength: event.target.value })}
                  placeholder="128000"
                />
              </label>
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("providers:capabilities")}</span>
                <Input
                  value={form.capabilities}
                  onChange={(event) => setForm({ ...form, capabilities: event.target.value })}
                  placeholder="text,vision"
                />
              </label>
            </div>
            <p className="text-xs text-muted-foreground">{t("providers:priceHint")}</p>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setForm(null)}>
                {t("common:cancel")}
              </Button>
              <Button
                onClick={() => void handleSave()}
                disabled={
                  form.modelId.trim() === "" || createModel.isPending || updateModel.isPending
                }
              >
                {t("common:save")}
              </Button>
            </div>
          </div>
        ) : null}
      </Dialog>
    </div>
  );
}
