import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Play, Save, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Input, Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { Badge } from "@/components/ui/Badge";
import { toast } from "@/components/ui/Toast";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import {
  useCreatePrototype,
  useDeletePrototype,
  usePrototypes,
  useUpdatePrototype,
} from "./api";
import { useProjects } from "@/features/projects/api";
import { formatDateTime } from "@/lib/format";

const starterHtml = `<div style="font-family: system-ui; padding: 24px;">
  <h1 style="margin: 0 0 8px;">Crew Pilot 原型</h1>
  <p style="color: #64748b; margin: 0;">在左侧编写 HTML，右侧实时预览。</p>
</div>`;

/** 原型工坊：HTML 编辑 + 沙箱 iframe 实时运行，来源不限（HTML/URL/图片/设计 MCP） */
export function PrototypeStudioPage() {
  const { t } = useTranslation(["prototype", "common"]);
  const projects = useProjects();
  const [projectId, setProjectId] = useState("");
  const activeProjectId = projectId === "" ? undefined : projectId;

  const prototypes = usePrototypes(activeProjectId);
  const createPrototype = useCreatePrototype(activeProjectId ?? "");
  const updatePrototype = useUpdatePrototype(activeProjectId ?? "");
  const deletePrototype = useDeletePrototype(activeProjectId ?? "");
  const confirm = useConfirm();

  const [name, setName] = useState("");
  const [type, setType] = useState("html");
  const [content, setContent] = useState(starterHtml);
  const [currentId, setCurrentId] = useState<string | null>(null);

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleSave = async () => {
    if (activeProjectId === undefined) {
      toast.error(t("prototype:selectProjectFirst"));
      return;
    }
    if (name.trim() === "" || content.trim() === "") {
      return;
    }
    try {
      if (currentId === null) {
        await createPrototype.mutateAsync({
          name: name.trim(),
          type,
          content,
        });
      } else {
        await updatePrototype.mutateAsync({
          id: currentId,
          name: name.trim(),
          content,
        });
      }
      toast.success(t("prototype:saved"));
      setCurrentId(null);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleLoad = (id: string) => {
    const prototype = prototypes.data?.find((item) => item.id === id);
    if (!prototype) {
      return;
    }
    setName(prototype.name);
    setType(prototype.type);
    setContent(prototype.content);
    setCurrentId(prototype.id);
  };

  const handleDelete = async (id: string) => {
    const approved = await confirm({
      title: t("prototype:deleteConfirm.title"),
      description: t("prototype:deleteConfirm.description"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deletePrototype.mutateAsync(id);
      toast.success(t("prototype:deleted"));
      if (currentId === id) {
        setCurrentId(null);
      }
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const projectOptions = (projects.data ?? []).map((project) => ({
    value: project.id,
    label: `${project.name}（${project.key}）`,
  }));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">{t("prototype:title")}</h1>
        <p className="text-sm text-muted-foreground">{t("prototype:description")}</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>{t("prototype:project")}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <Select
              value={projectId}
              onValueChange={(value) => {
                setProjectId(value);
                setCurrentId(null);
              }}
              options={projectOptions}
              placeholder={t("prototype:selectProject")}
              ariaLabel={t("prototype:project")}
            />
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("prototype:name")}</span>
              <Input
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder={t("prototype:namePlaceholder")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("prototype:type")}</span>
              <Select
                value={type}
                onValueChange={setType}
                options={[
                  { value: "html", label: t("prototype:types.html") },
                  { value: "url", label: t("prototype:types.url") },
                  { value: "image", label: t("prototype:types.image") },
                  { value: "mcp", label: t("prototype:types.mcp") },
                ]}
                ariaLabel={t("prototype:type")}
              />
            </label>
            <Button
              className="w-full"
              onClick={() => void handleSave()}
              disabled={activeProjectId === undefined || name.trim() === ""}
            >
              <Save className="h-4 w-4" />
              {currentId === null ? t("prototype:save") : t("common:save")}
            </Button>
          </CardContent>
        </Card>

        <div className="grid gap-4 xl:grid-cols-2">
          <Card className="flex flex-col">
            <CardHeader>
              <CardTitle>{t("prototype:editor")}</CardTitle>
            </CardHeader>
            <CardContent className="flex-1">
              <Textarea
                value={content}
                onChange={(event) => setContent(event.target.value)}
                placeholder={t("prototype:editorPlaceholder")}
                className="h-80 font-mono text-xs"
              />
            </CardContent>
          </Card>

          <Card className="flex flex-col">
            <CardHeader className="flex-row items-center justify-between">
              <CardTitle className="flex items-center gap-2">
                <Play className="h-4 w-4" />
                {t("prototype:preview")}
              </CardTitle>
              <Badge tone="info">{t("prototype:runHint")}</Badge>
            </CardHeader>
            <CardContent className="flex-1">
              {type === "html" ? (
                <iframe
                  title={t("prototype:preview")}
                  sandbox="allow-scripts"
                  srcDoc={content}
                  className="h-80 w-full rounded-md border border-border bg-white"
                />
              ) : (
                <div className="flex h-80 items-center justify-center rounded-md border border-dashed border-border p-4 text-center text-sm text-muted-foreground">
                  {content.trim() === "" ? "—" : content}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      <div className="space-y-3">
        <h2 className="text-base font-semibold">{t("prototype:savedList")}</h2>
        {activeProjectId === undefined ? (
          <EmptyState>{t("prototype:selectProjectFirst")}</EmptyState>
        ) : prototypes.isPending ? (
          <LoadingState />
        ) : prototypes.isError ? (
          <ErrorState onRetry={() => void prototypes.refetch()} />
        ) : (prototypes.data ?? []).length === 0 ? (
          <EmptyState>{t("prototype:empty")}</EmptyState>
        ) : (
          <div className="grid gap-3 grid-cols-[repeat(auto-fill,minmax(240px,1fr))]">
            {(prototypes.data ?? []).map((prototype) => (
              <Card key={prototype.id}>
                <CardHeader>
                  <CardTitle className="truncate">{prototype.name}</CardTitle>
                  <p className="text-xs text-muted-foreground">
                    v{prototype.version} · {formatDateTime(prototype.updatedAt)}
                  </p>
                </CardHeader>
                <CardContent className="flex items-center justify-between">
                  <Badge tone="neutral">{t(`prototype:types.${prototype.type}`)}</Badge>
                  <div className="flex gap-1">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleLoad(prototype.id)}
                    >
                      {t("prototype:load")}
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={t("common:delete")}
                      onClick={() => void handleDelete(prototype.id)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
