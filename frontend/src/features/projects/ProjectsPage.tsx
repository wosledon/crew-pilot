import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/Card";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { toast } from "@/components/ui/Toast";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { useCreateProject, useDeleteProject, useProjects } from "./api";
import { formatDateTime } from "@/lib/format";

export function ProjectsPage() {
  const { t } = useTranslation(["projects", "common"]);
  const projects = useProjects();
  const createProject = useCreateProject();
  const deleteProject = useDeleteProject();
  const confirm = useConfirm();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [key, setKey] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [repoUrl, setRepoUrl] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleCreate = async () => {
    try {
      await createProject.mutateAsync({
        key: key.trim().toUpperCase(),
        name: name.trim(),
        description: description.trim() || undefined,
        repoUrl: repoUrl.trim() || undefined,
      });
      toast.success(t("projects:created"));
      setDialogOpen(false);
      setKey("");
      setName("");
      setDescription("");
      setRepoUrl("");
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
                <CardTitle>
                  <Link to={`/projects/${project.id}`} className="hover:underline">
                    {project.name}
                  </Link>
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
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={t("common:delete")}
                  onClick={() => void handleDelete(project.id)}
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
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
    </div>
  );
}
