import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useProject, useUpdateProject } from "../api";

export interface ProjectEditDialogProps {
  projectId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

interface ProjectForm {
  name: string;
  description: string;
  repoUrl: string;
  localPath: string;
}

/** 编辑项目基本信息（名称/描述/仓库/本地工作目录）。 */
export function ProjectEditDialog({ projectId, open, onOpenChange }: ProjectEditDialogProps) {
  const { t } = useTranslation(["projects", "common"]);
  const project = useProject(projectId);
  const updateProject = useUpdateProject();
  const [form, setForm] = useState<ProjectForm | null>(null);

  useEffect(() => {
    if (project.data !== undefined && form === null) {
      setForm({
        name: project.data.name,
        description: project.data.description ?? "",
        repoUrl: project.data.repoUrl ?? "",
        localPath: project.data.localPath ?? "",
      });
    }
  }, [project.data, form]);

  const handleSubmit = async () => {
    if (form === null) {
      return;
    }
    try {
      await updateProject.mutateAsync({
        id: projectId,
        name: form.name.trim(),
        description: form.description.trim() || undefined,
        repoUrl: form.repoUrl.trim() || undefined,
        localPath: form.localPath.trim(),
      });
      toast.success(t("projects:updated"));
      onOpenChange(false);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("projects:editTitle")}
      closeLabel={t("common:cancel")}
    >
      {project.isPending || form === null ? (
        <LoadingState />
      ) : (
        <div className="space-y-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:name")}</span>
            <Input
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:repoUrl")}</span>
            <Input
              value={form.repoUrl}
              onChange={(event) => setForm({ ...form, repoUrl: event.target.value })}
              placeholder={t("projects:repoUrlPlaceholder")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:localPath")}</span>
            <Input
              value={form.localPath}
              onChange={(event) => setForm({ ...form, localPath: event.target.value })}
              placeholder={t("projects:localPathPlaceholder")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:descriptionLabel")}</span>
            <Textarea
              value={form.description}
              onChange={(event) => setForm({ ...form, description: event.target.value })}
            />
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleSubmit()}
              disabled={updateProject.isPending || form.name.trim() === ""}
            >
              {t("common:save")}
            </Button>
          </div>
        </div>
      )}
    </Dialog>
  );
}
