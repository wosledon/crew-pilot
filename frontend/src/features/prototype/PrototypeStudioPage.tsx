import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ExternalLink, History, Play, RotateCcw, Save, Trash2, Upload } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Input, Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { Badge } from "@/components/ui/Badge";
import { toast } from "@/components/ui/Toast";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import {
  prototypeImageUrl,
  useCreatePrototype,
  useDeletePrototype,
  useDeletePrototypeFile,
  useDraftRequirement,
  usePrototypeFiles,
  usePrototypeVersions,
  usePrototypes,
  useRestorePrototypeVersion,
  useUpdatePrototype,
  useUploadPrototypeImage,
  useUpsertPrototypeFile,
} from "./api";
import { buildSandboxDocument } from "./sandboxDocument";
import { useProjects, useWorkItems } from "@/features/projects/api";
import { formatDateTime } from "@/lib/format";

const starterHtml = `<div style="font-family: system-ui; padding: 24px;">
  <h1 style="margin: 0 0 8px;">Crew Pilot 原型</h1>
  <p style="color: #64748b; margin: 0;">在左侧编写 HTML，右侧实时预览。</p>
</div>`;

const typeDefaults: Record<string, string> = {
  html: starterHtml,
  url: "https://",
  image: "",
  mcp: "",
};

/** 原型工坊：HTML / URL / 图片 / MCP 引用；保存留版本快照，可恢复。 */
export function PrototypeStudioPage() {
  const { t } = useTranslation(["prototype", "common"]);
  const projects = useProjects();
  const [projectId, setProjectId] = useState("");
  const activeProjectId = projectId === "" ? undefined : projectId;

  const prototypes = usePrototypes(activeProjectId);
  const requirements = useWorkItems(activeProjectId, "requirement");
  const createPrototype = useCreatePrototype(activeProjectId ?? "");
  const updatePrototype = useUpdatePrototype(activeProjectId ?? "");
  const deletePrototype = useDeletePrototype(activeProjectId ?? "");
  const uploadImage = useUploadPrototypeImage(activeProjectId ?? "");
  const restoreVersion = useRestorePrototypeVersion(activeProjectId ?? "");
  const draftRequirement = useDraftRequirement(activeProjectId ?? "");
  const confirm = useConfirm();

  const [name, setName] = useState("");
  const [type, setType] = useState("html");
  const [content, setContent] = useState(starterHtml);
  const [requirementId, setRequirementId] = useState("");
  const [currentId, setCurrentId] = useState<string | null>(null);
  const versions = usePrototypeVersions(currentId);
  const files = usePrototypeFiles(currentId ?? undefined);
  const upsertFile = useUpsertPrototypeFile(currentId ?? "");
  const deleteFile = useDeletePrototypeFile(currentId ?? "");
  const [filePath, setFilePath] = useState("index.html");
  const [fileContent, setFileContent] = useState("");
  const [editingFileId, setEditingFileId] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);

  const hasIndexHtml = (files.data ?? []).some((file) => file.path === "index.html");

  const handleDraftRequirement = async () => {
    if (currentId === null) {
      return;
    }
    try {
      const requirement = await draftRequirement.mutateAsync(currentId);
      toast.success(t("prototype:requirementDrafted", { title: requirement.title }));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleSaveFile = async () => {
    if (currentId === null || filePath.trim() === "") {
      return;
    }
    try {
      await upsertFile.mutateAsync({
        path: filePath.trim(),
        content: fileContent,
      });
      toast.success(t("prototype:fileSaved"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDeleteFile = async (fileId: string) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: t("prototype:deleteFileConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteFile.mutateAsync(fileId);
      if (editingFileId === fileId) {
        setEditingFileId(null);
        setFileContent("");
      }
      toast.success(t("prototype:fileDeleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

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
          requirementId: requirementId === "" ? undefined : requirementId,
        });
      } else {
        await updatePrototype.mutateAsync({
          id: currentId,
          name: name.trim(),
          content,
          requirementId: requirementId === "" ? undefined : requirementId,
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
    setRequirementId(prototype.requirementId ?? "");
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

  const handleTypeChange = (value: string) => {
    setType(value);
    if (currentId === null) {
      setContent(typeDefaults[value] ?? "");
    }
  };

  const handleImageUpload = async (file: File | undefined) => {
    if (file === undefined || activeProjectId === undefined) {
      return;
    }
    setUploading(true);
    try {
      const image = await uploadImage.mutateAsync(file);
      setContent(image.path);
      toast.success(t("prototype:imageUploaded"));
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setUploading(false);
    }
  };

  const handleRestore = async (version: number) => {
    if (currentId === null) {
      return;
    }
    try {
      const restored = await restoreVersion.mutateAsync({ id: currentId, version });
      setName(restored.name);
      setContent(restored.content);
      setRequirementId(restored.requirementId ?? "");
      toast.success(t("prototype:restored", { version }));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const projectOptions = (projects.data ?? []).map((project) => ({
    value: project.id,
    label: `${project.name}（${project.key}）`,
  }));
  const requirementOptions = (requirements.data ?? []).map((requirement) => ({
    value: requirement.id,
    label: requirement.title,
  }));

  const urlValid =
    type === "url" &&
    (() => {
      try {
        const parsed = new URL(content);
        return parsed.protocol === "http:" || parsed.protocol === "https:";
      } catch {
        return false;
      }
    })();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">{t("prototype:title")}</h1>
        <p className="text-sm text-muted-foreground">{t("prototype:description")}</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-[280px_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>{t("prototype:project")}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <Select
              value={projectId}
              onValueChange={(value) => {
                setProjectId(value);
                setCurrentId(null);
                setRequirementId("");
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
                onValueChange={handleTypeChange}
                options={[
                  { value: "html", label: t("prototype:types.html") },
                  { value: "url", label: t("prototype:types.url") },
                  { value: "image", label: t("prototype:types.image") },
                  { value: "mcp", label: t("prototype:types.mcp") },
                ]}
                ariaLabel={t("prototype:type")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("prototype:requirement")}</span>
              <Select
                value={requirementId}
                onValueChange={setRequirementId}
                options={[
                  { value: "", label: t("prototype:noRequirement") },
                  ...requirementOptions,
                ]}
                ariaLabel={t("prototype:requirement")}
              />
            </label>
            {type === "image" ? (
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("prototype:uploadImage")}</span>
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp,image/gif"
                  disabled={uploading}
                  onChange={(event) => {
                    void handleImageUpload(event.target.files?.[0]);
                    event.target.value = "";
                  }}
                  className="block w-full text-xs text-muted-foreground file:mr-2 file:rounded-md file:border-0 file:bg-accent file:px-3 file:py-1.5 file:text-accent-foreground"
                />
                <span className="text-xs text-muted-foreground">{t("prototype:imageHint")}</span>
              </label>
            ) : null}
            <Button
              className="w-full"
              onClick={() => void handleSave()}
              disabled={
                activeProjectId === undefined || name.trim() === "" || content.trim() === ""
              }
            >
              <Save className="h-4 w-4" />
              {currentId === null ? t("prototype:save") : t("common:save")}
            </Button>
            {currentId !== null ? (
              <Button
                className="w-full"
                variant="outline"
                disabled={draftRequirement.isPending}
                onClick={() => void handleDraftRequirement()}
              >
                {t("prototype:draftRequirement")}
              </Button>
            ) : null}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <div className="grid gap-4 xl:grid-cols-2">
            <Card className="flex flex-col">
              <CardHeader>
                <CardTitle>{t("prototype:editor")}</CardTitle>
              </CardHeader>
              <CardContent className="flex-1">
                {type === "image" ? (
                  <div className="space-y-2">
                    <p className="text-xs text-muted-foreground">
                      {t("prototype:imageEditorHint")}
                    </p>
                    <Input
                      value={content}
                      onChange={(event) => setContent(event.target.value)}
                      placeholder={t("prototype:imagePathPlaceholder")}
                    />
                  </div>
                ) : (
                  <>
                    <Textarea
                      value={content}
                      onChange={(event) => setContent(event.target.value)}
                      placeholder={t("prototype:editorPlaceholder")}
                      className="h-80 font-mono text-xs"
                    />
                    <p className="mt-2 text-xs text-muted-foreground">{t("prototype:cspHint")}</p>
                  </>
                )}
              </CardContent>
            </Card>

            <Card className="flex flex-col">
              <CardHeader className="flex-row items-center justify-between">
                <CardTitle className="flex items-center gap-2">
                  <Play className="h-4 w-4" />
                  {t("prototype:preview")}
                </CardTitle>
                <Badge tone="info">{t(`prototype:types.${type}`)}</Badge>
              </CardHeader>
              <CardContent className="flex-1">
                {type === "html" ? (
                  hasIndexHtml && currentId !== null ? (
                    <iframe
                      title={t("prototype:preview")}
                      sandbox="allow-scripts"
                      src={`/api/prototypes/${currentId}/preview/index.html`}
                      className="h-80 w-full rounded-md border border-border bg-white"
                    />
                  ) : (
                    <iframe
                      title={t("prototype:preview")}
                      sandbox="allow-scripts"
                      srcDoc={buildSandboxDocument(content)}
                      className="h-80 w-full rounded-md border border-border bg-white"
                    />
                  )
                ) : type === "url" ? (
                  urlValid ? (
                    <div className="space-y-2">
                      <iframe
                        title={t("prototype:preview")}
                        sandbox=""
                        src={content}
                        className="h-72 w-full rounded-md border border-border bg-white"
                      />
                      <a
                        href={content}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                      >
                        <ExternalLink className="h-3.5 w-3.5" />
                        {t("prototype:openExternal")}
                      </a>
                    </div>
                  ) : (
                    <div className="flex h-80 items-center justify-center rounded-md border border-dashed border-border p-4 text-center text-sm text-muted-foreground">
                      {t("prototype:invalidUrl")}
                    </div>
                  )
                ) : type === "image" && content.trim() !== "" ? (
                  <img
                    src={prototypeImageUrl(content)}
                    alt={name}
                    className="max-h-80 w-full rounded-md border border-border object-contain"
                  />
                ) : type === "mcp" ? (
                  <div className="space-y-2 rounded-md border border-border p-4 text-sm">
                    <p className="text-muted-foreground">{t("prototype:mcpHint")}</p>
                    {/^https?:\/\//i.test(content) ? (
                      <a
                        href={content}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1 text-primary hover:underline"
                      >
                        <ExternalLink className="h-3.5 w-3.5" />
                        {content}
                      </a>
                    ) : (
                      <pre className="overflow-x-auto whitespace-pre-wrap text-xs text-muted-foreground">
                        {content}
                      </pre>
                    )}
                  </div>
                ) : (
                  <div className="flex h-80 items-center justify-center rounded-md border border-dashed border-border p-4 text-center text-sm text-muted-foreground">
                    {t("prototype:uploadFirst")}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          {currentId !== null && type === "html" ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("prototype:files")}</CardTitle>
                <p className="text-xs text-muted-foreground">{t("prototype:filesHint")}</p>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex flex-wrap gap-2">
                  {(files.data ?? []).map((file) => (
                    <span key={file.id} className="flex items-center gap-1">
                      <Button
                        size="sm"
                        variant={editingFileId === file.id ? "primary" : "outline"}
                        onClick={() => {
                          setEditingFileId(file.id);
                          setFilePath(file.path);
                          setFileContent(file.content);
                        }}
                      >
                        {file.path}
                      </Button>
                      <Button
                        size="icon"
                        variant="ghost"
                        aria-label={t("common:delete")}
                        onClick={() => void handleDeleteFile(file.id)}
                      >
                        <Trash2 className="h-3 w-3 text-destructive" />
                      </Button>
                    </span>
                  ))}
                  {(files.data ?? []).length === 0 ? (
                    <p className="text-sm text-muted-foreground">{t("prototype:noFiles")}</p>
                  ) : null}
                </div>
                <div className="grid gap-2 sm:grid-cols-2">
                  <label className="block space-y-1 text-sm">
                    <span className="font-medium">{t("prototype:filePath")}</span>
                    <Input
                      value={filePath}
                      onChange={(event) => {
                        setFilePath(event.target.value);
                        setEditingFileId(null);
                      }}
                      placeholder="index.html / styles.css"
                    />
                  </label>
                  <div className="flex items-end">
                    <Button
                      size="sm"
                      onClick={() => void handleSaveFile()}
                      disabled={upsertFile.isPending || filePath.trim() === ""}
                    >
                      <Save className="h-3.5 w-3.5" />
                      {t("prototype:saveFile")}
                    </Button>
                  </div>
                </div>
                <Textarea
                  value={fileContent}
                  onChange={(event) => setFileContent(event.target.value)}
                  placeholder={t("prototype:fileEditorPlaceholder")}
                  className="min-h-40 font-mono text-xs"
                />
              </CardContent>
            </Card>
          ) : null}

          {currentId !== null ? (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <History className="h-4 w-4" />
                  {t("prototype:versions")}
                </CardTitle>
              </CardHeader>
              <CardContent>
                {versions.isPending ? (
                  <LoadingState />
                ) : versions.isError ? (
                  <ErrorState onRetry={() => void versions.refetch()} />
                ) : (versions.data ?? []).length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t("prototype:noVersions")}</p>
                ) : (
                  <ul className="divide-y divide-border">
                    {(versions.data ?? []).map((version) => (
                      <li
                        key={version.id}
                        className="flex items-center justify-between gap-2 py-2 text-sm"
                      >
                        <span className="min-w-0">
                          <span className="font-medium">v{version.version}</span>
                          <span className="ml-2 text-muted-foreground">
                            {formatDateTime(version.createdAt)}
                          </span>
                        </span>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => void handleRestore(version.version)}
                          disabled={restoreVersion.isPending}
                        >
                          <RotateCcw className="h-3.5 w-3.5" />
                          {t("prototype:restore")}
                        </Button>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          ) : null}
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
                    <Button variant="outline" size="sm" onClick={() => handleLoad(prototype.id)}>
                      <Upload className="h-3.5 w-3.5" />
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
