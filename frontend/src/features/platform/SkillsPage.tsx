import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Download, Pencil, Plus, Trash2, Upload } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import type { Skill } from "@/lib/types";
import { useCreateSkill, useDeleteSkill, useImportSkill, useSkills, useUpdateSkill } from "./api";

interface SkillForm {
  id?: string;
  name: string;
  description: string;
  promptContent: string;
  tools: string;
  scriptPath: string;
  enabled: boolean;
}

const emptyForm: SkillForm = {
  name: "",
  description: "",
  promptContent: "",
  tools: "",
  scriptPath: "",
  enabled: true,
};

export function SkillsPage() {
  const { t } = useTranslation(["skills", "common"]);
  const skills = useSkills();
  const createSkill = useCreateSkill();
  const updateSkill = useUpdateSkill();
  const deleteSkill = useDeleteSkill();
  const importSkill = useImportSkill();
  const confirm = useConfirm();

  const [form, setForm] = useState<SkillForm | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [importContent, setImportContent] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const openCreate = () => setForm({ ...emptyForm });

  const openEdit = (skill: Skill) =>
    setForm({
      id: skill.id,
      name: skill.name,
      description: skill.description ?? "",
      promptContent: skill.promptContent,
      tools: skill.tools ?? "",
      scriptPath: skill.scriptPath ?? "",
      enabled: skill.enabled,
    });

  const handleSave = async () => {
    if (form === null) {
      return;
    }
    try {
      if (form.id === undefined) {
        await createSkill.mutateAsync({
          name: form.name.trim(),
          description: form.description.trim() || undefined,
          promptContent: form.promptContent,
          tools: form.tools.trim() || undefined,
          scriptPath: form.scriptPath.trim() || undefined,
        });
        toast.success(t("skills:created"));
      } else {
        await updateSkill.mutateAsync({
          id: form.id,
          name: form.name.trim(),
          description: form.description.trim() || undefined,
          promptContent: form.promptContent,
          tools: form.tools.trim() || undefined,
          scriptPath: form.scriptPath.trim() || undefined,
          enabled: form.enabled,
        });
        toast.success(t("skills:updated"));
      }
      setForm(null);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async (skill: Skill) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: t("skills:deleteConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteSkill.mutateAsync(skill.id);
      toast.success(t("skills:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleImport = async () => {
    if (importContent.trim() === "") {
      return;
    }
    try {
      const skill = await importSkill.mutateAsync(importContent);
      toast.success(t("skills:imported", { name: skill.name }));
      setImportOpen(false);
      setImportContent("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleExport = (skill: Skill) => {
    const anchor = document.createElement("a");
    anchor.href = `/api/skills/${skill.id}/export`;
    anchor.download = `${skill.name}.SKILL.md`;
    anchor.click();
  };

  const handleImportFile = async (file: File | undefined) => {
    if (file === undefined) {
      return;
    }
    setImportContent(await file.text());
  };

  if (skills.isPending) {
    return <LoadingState />;
  }

  if (skills.isError) {
    return <ErrorState onRetry={() => void skills.refetch()} />;
  }

  const items = skills.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t("skills:title")}</h1>
          <p className="text-sm text-muted-foreground">{t("skills:description")}</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setImportOpen(true)}>
            <Upload className="h-4 w-4" />
            {t("skills:import")}
          </Button>
          <Button onClick={openCreate}>
            <Plus className="h-4 w-4" />
            {t("skills:create")}
          </Button>
        </div>
      </div>

      {items.length === 0 ? (
        <EmptyState>{t("skills:empty")}</EmptyState>
      ) : (
        <div className="grid gap-4 grid-cols-[repeat(auto-fill,minmax(280px,1fr))]">
          {items.map((skill) => (
            <Card key={skill.id}>
              <CardHeader>
                <CardTitle className="flex items-center justify-between gap-2">
                  <span className="truncate">{skill.name}</span>
                  <Badge tone={skill.enabled ? "success" : "neutral"}>
                    {skill.enabled ? t("skills:enabled") : t("common:empty")}
                  </Badge>
                </CardTitle>
                <p className="text-xs text-muted-foreground">
                  {t("skills:agentCount", { count: skill.agentCount })}
                </p>
              </CardHeader>
              <CardContent className="space-y-3">
                <p className="line-clamp-3 text-sm text-muted-foreground">
                  {skill.description ?? skill.promptContent}
                </p>
                <div className="flex gap-2">
                  <Button size="sm" variant="outline" onClick={() => openEdit(skill)}>
                    <Pencil className="h-3.5 w-3.5" />
                    {t("common:save")}
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    aria-label={t("skills:export")}
                    onClick={() => handleExport(skill)}
                  >
                    <Download className="h-3.5 w-3.5" />
                    {t("skills:export")}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDelete(skill)}
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
        open={importOpen}
        onOpenChange={(open) => {
          setImportOpen(open);
          if (!open) {
            setImportContent("");
          }
        }}
        title={t("skills:importTitle")}
        closeLabel={t("common:cancel")}
        className="max-w-2xl"
      >
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">{t("skills:importHint")}</p>
          <input
            type="file"
            accept=".md,.markdown,text/markdown"
            aria-label={t("skills:importFile")}
            onChange={(event) => void handleImportFile(event.target.files?.[0])}
          />
          <Textarea
            value={importContent}
            onChange={(event) => setImportContent(event.target.value)}
            placeholder={t("skills:importPlaceholder")}
            className="min-h-56 font-mono text-xs"
          />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setImportOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleImport()}
              disabled={importSkill.isPending || importContent.trim() === ""}
            >
              <Upload className="h-4 w-4" />
              {t("skills:import")}
            </Button>
          </div>
        </div>
      </Dialog>

      <Dialog
        open={form !== null}
        onOpenChange={(open) => {
          if (!open) {
            setForm(null);
          }
        }}
        title={form?.id === undefined ? t("skills:createTitle") : t("skills:editTitle")}
        closeLabel={t("common:cancel")}
        className="max-w-2xl"
      >
        {form !== null ? (
          <div className="space-y-4">
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("skills:name")}</span>
              <Input
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
                placeholder={t("skills:namePlaceholder")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("skills:descriptionLabel")}</span>
              <Input
                value={form.description}
                onChange={(event) => setForm({ ...form, description: event.target.value })}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("skills:promptContent")}</span>
              <Textarea
                value={form.promptContent}
                onChange={(event) => setForm({ ...form, promptContent: event.target.value })}
                placeholder={t("skills:promptPlaceholder")}
                className="min-h-28"
              />
            </label>
            <div className="grid grid-cols-2 gap-4">
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("skills:tools")}</span>
                <Input
                  value={form.tools}
                  onChange={(event) => setForm({ ...form, tools: event.target.value })}
                />
              </label>
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("skills:scriptPath")}</span>
                <Input
                  value={form.scriptPath}
                  onChange={(event) => setForm({ ...form, scriptPath: event.target.value })}
                />
              </label>
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setForm(null)}>
                {t("common:cancel")}
              </Button>
              <Button
                onClick={() => void handleSave()}
                disabled={
                  form.name.trim() === "" ||
                  form.promptContent.trim() === "" ||
                  createSkill.isPending ||
                  updateSkill.isPending
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
