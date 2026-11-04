import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { Archive, ArchiveRestore, Pencil, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/Card";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { toast } from "@/components/ui/Toast";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { ProjectEditDialog } from "./components/ProjectEditDialog";
import { useArchiveProject, useCreateProject, useDeleteProject, useProjects } from "./api";
import { formatDateTime } from "@/lib/format";

export function ProjectsPage() {
  const { t } = useTranslation(["projects", "common"]);
  const projects = useProjects();
  const createProject = useCreateProject();
  const deleteProject = useDeleteProject();
  const archiveProject = useArchiveProject();
  const confirm = useConfirm();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [key, setKey] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [repoUrl, setRepoUrl] = useState("");
  const [localPath, setLocalPath] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleCreate = async () => {
    try {
      await createProject.mutateAsync({
        key: key.trim().toUpperCase(),
        name: name.trim(),
        description: description.trim() || undefined,
        repoUrl: repoUrl.trim() || undefined,
        localPath: localPath.trim() || undefined,
      });
      toast.success(t("projects:created"));
      setDialogOpen(false);
      setKey("");
      setName("");
      setDescription("");
      setRepoUrl("");
      setLocalPath("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async (id: string) => {
    const approved = await confirm({
      title: t("projects:deleteConfirm.title"),
      description: t("projects:deleteConfirm.description"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteProject.mutateAsync(id);
      toast.success(t("projects:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleArchive = async (id: string, archive: boolean) => {
    try {
      await archiveProject.mutateAsync({ id, archive });
      toast.success(archive ? t("projects:archived") : t("projects:unarchived"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (projects.isPending) {
    return <LoadingState />;
  }

  if (projects.isError) {
    return <ErrorState onRetry={() => void projects.refetch()} />;
  }

  const items = projects.data;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t("projects:title")}</h1>
          <p className="text-sm text-muted-foreground">{t("projects:description")}</p>
        </div>
        <Button onClick={() => setDialogOpen(true)}>
          <Plus className="h-4 w-4" />
          {t("projects:create")}
        </Button>
      </div>

      {items.length === 0 ? (
        <EmptyState>{t("projects:empty")}</EmptyState>
      ) : (
        <div className="grid gap-4 grid-cols-[repeat(auto-fill,minmax(280px,1fr))]">
          {items.map((project) => (
            <Card key={project.id}>
              <CardHeader>
                <CardTitle className="flex items-center justify-between gap-2">
                  <Link to={`/projects/${project.id}`} className="truncate hover:underline">
                    {project.name}
                  </Link>
                  {project.status === "archived" ? (
                    <Badge tone="warning">{t("projects:archivedBadge")}</Badge>
                  ) : null}
                </CardTitle>
                <CardDescription>
                  {project.key} · {t("projects:createdAt")} {formatDateTime(project.createdAt)}
                </CardDescription>
              </CardHeader>
              <CardContent className="truncate text-sm text-muted-foreground">
                {project.repoUrl ?? "—"}
              </CardContent>
              <CardFooter className="justify-between">
                <span className="text-sm text-muted-foreground">
                  {project.workItemCount} {t("projects:workItemCount")}
                </span>
                <div className="flex items-center gap-1">
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={t("projects:edit")}
                    onClick={() => setEditingId(project.id)}
                  >
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={
                      project.status === "archived"
                        ? t("projects:unarchive")
                        : t("projects:archive")
                    }
                    onClick={() => void handleArchive(project.id, project.status !== "archived")}
                  >
                    {project.status === "archived" ? (
                      <ArchiveRestore className="h-4 w-4" />
                    ) : (
                      <Archive className="h-4 w-4" />
                    )}
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDelete(project.id)}
                  >
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              </CardFooter>
            </Card>
          ))}
        </div>
      )}

      <Dialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        title={t("projects:createTitle")}
        closeLabel={t("common:cancel")}
      >
        <div className="space-y-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:key")}</span>
            <Input
              value={key}
              onChange={(event) => setKey(event.target.value)}
              placeholder={t("projects:keyPlaceholder")}
              maxLength={32}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:name")}</span>
            <Input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={t("projects:namePlaceholder")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:repoUrl")}</span>
            <Input
              value={repoUrl}
              onChange={(event) => setRepoUrl(event.target.value)}
              placeholder={t("projects:repoUrlPlaceholder")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:localPath")}</span>
            <Input
              value={localPath}
              onChange={(event) => setLocalPath(event.target.value)}
              placeholder={t("projects:localPathPlaceholder")}
            />
            <span className="text-xs text-muted-foreground">{t("projects:localPathHint")}</span>
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("projects:descriptionLabel")}</span>
            <Textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleCreate()}
              disabled={createProject.isPending || key.trim() === "" || name.trim() === ""}
            >
              {t("common:create")}
            </Button>
          </div>
        </div>
      </Dialog>

      {editingId !== null ? (
        <ProjectEditDialog
          open
          projectId={editingId}
          onOpenChange={(open) => {
            if (!open) {
              setEditingId(null);
            }
          }}
        />
      ) : null}
    </div>
  );
}
