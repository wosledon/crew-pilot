import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { toast } from "@/components/ui/Toast";
import type { WorkItemDetail } from "@/lib/types";
import { useIterations, useProjectMembers, useUpdateWorkItem, useWorkItems } from "../api";

export interface EditWorkItemDialogProps {
  item: WorkItemDetail;
  projectId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** 编辑工作项：字段、指派人、迭代、截止日期、标签、Task 父项与缺陷字段。 */
export function EditWorkItemDialog({
  item,
  projectId,
  open,
  onOpenChange,
}: EditWorkItemDialogProps) {
  const { t } = useTranslation(["workitems", "common", "projects"]);
  const updateWorkItem = useUpdateWorkItem();
  const members = useProjectMembers(projectId);
  const iterations = useIterations(projectId);
  const workItems = useWorkItems(projectId);

  const [title, setTitle] = useState(item.title);
  const [description, setDescription] = useState(item.description ?? "");
  const [acceptanceCriteria, setAcceptanceCriteria] = useState(item.acceptanceCriteria ?? "");
  const [priority, setPriority] = useState(item.priority);
  const [estimatedHours, setEstimatedHours] = useState(
    item.estimatedHours === null ? "" : String(item.estimatedHours),
  );
  const [remainingHours, setRemainingHours] = useState(
    item.remainingHours === null ? "" : String(item.remainingHours),
  );
  const [storyPoints, setStoryPoints] = useState(
    item.storyPoints === null ? "" : String(item.storyPoints),
  );
  const [dueDate, setDueDate] = useState(item.dueDate ?? "");
  const [iterationId, setIterationId] = useState(item.iterationId ?? "");
  const [assigneeName, setAssigneeName] = useState(item.assigneeName ?? "");
  const [tags, setTags] = useState(item.tags ?? "");
  const [severity, setSeverity] = useState(item.severity ?? "normal");
  const [reproSteps, setReproSteps] = useState(item.reproSteps ?? "");
  const [environment, setEnvironment] = useState(item.environment ?? "");
  const [parentWorkItemId, setParentWorkItemId] = useState(item.parentWorkItemId ?? "");
  const [branchUrl, setBranchUrl] = useState(item.branchUrl ?? "");
  const [relatedRequirementId, setRelatedRequirementId] = useState(item.relatedRequirementId ?? "");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleSubmit = async () => {
    try {
      await updateWorkItem.mutateAsync({
        id: item.id,
        body: {
          title: title.trim(),
          description: description.trim() || undefined,
          acceptanceCriteria: acceptanceCriteria.trim() || undefined,
          priority,
          storyPoints: storyPoints === "" ? undefined : Number(storyPoints),
          estimatedHours: estimatedHours === "" ? undefined : Number(estimatedHours),
          remainingHours: remainingHours === "" ? undefined : Number(remainingHours),
          dueDate: dueDate || undefined,
          iterationId: iterationId || undefined,
          assigneeName: assigneeName || undefined,
          tags: tags.trim() || undefined,
          severity: item.type === "defect" ? severity : undefined,
          reproSteps: item.type === "defect" ? reproSteps.trim() || undefined : undefined,
          environment: item.type === "defect" ? environment.trim() || undefined : undefined,
          parentWorkItemId: item.type === "task" ? parentWorkItemId || undefined : undefined,
          branchUrl: branchUrl.trim() || undefined,
          relatedRequirementId:
            item.type === "defect" ? relatedRequirementId || undefined : undefined,
        },
      });
      toast.success(t("workitems:updated"));
      onOpenChange(false);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const parentOptions = (workItems.data ?? [])
    .filter((candidate) => candidate.id !== item.id && candidate.type !== "task")
    .map((candidate) => ({
      value: candidate.id,
      label: `${t(`workitems:types.${candidate.type}`)} · ${candidate.title}`,
    }));

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("workitems:editTitle")}
      closeLabel={t("common:cancel")}
      className="max-w-2xl"
    >
      <div className="space-y-4">
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:title")}</span>
          <Input value={title} onChange={(event) => setTitle(event.target.value)} />
        </label>

        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:description")}</span>
          <Textarea value={description} onChange={(event) => setDescription(event.target.value)} />
        </label>

        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:acceptanceCriteria")}</span>
          <Textarea
            value={acceptanceCriteria}
            onChange={(event) => setAcceptanceCriteria(event.target.value)}
          />
        </label>

        <div className="grid grid-cols-2 gap-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workitems:priority")}</span>
            <Select
              value={priority}
              onValueChange={setPriority}
              options={[
                { value: "p0", label: t("workitems:priorities.p0") },
                { value: "p1", label: t("workitems:priorities.p1") },
                { value: "p2", label: t("workitems:priorities.p2") },
                { value: "p3", label: t("workitems:priorities.p3") },
              ]}
              ariaLabel={t("workitems:priority")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workitems:storyPoints")}</span>
            <Input
              type="number"
              min={0}
              value={storyPoints}
              onChange={(event) => setStoryPoints(event.target.value)}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("common:estimatedHours")}</span>
            <Input
              type="number"
              min={0}
              step={0.5}
              value={estimatedHours}
              onChange={(event) => setEstimatedHours(event.target.value)}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("common:remainingHours")}</span>
            <Input
              type="number"
              min={0}
              step={0.5}
              value={remainingHours}
              onChange={(event) => setRemainingHours(event.target.value)}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workitems:dueDate")}</span>
            <Input
              type="date"
              value={dueDate}
              onChange={(event) => setDueDate(event.target.value)}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workitems:iteration")}</span>
            <Select
              value={iterationId}
              onValueChange={setIterationId}
              options={[
                { value: "", label: t("workitems:iterationNone") },
                ...(iterations.data ?? []).map((iteration) => ({
                  value: iteration.id,
                  label: iteration.name,
                })),
              ]}
              ariaLabel={t("workitems:iteration")}
            />
          </label>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workitems:assignee")}</span>
            <Select
              value={assigneeName}
              onValueChange={setAssigneeName}
              options={[
                { value: "", label: "—" },
                ...(members.data ?? []).map((member) => ({
                  value: member.displayName,
                  label: `${member.displayName}（${t(`projects:roles.${member.role}`)}）`,
                })),
              ]}
              ariaLabel={t("workitems:assignee")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workitems:tags")}</span>
            <Input
              value={tags}
              onChange={(event) => setTags(event.target.value)}
              placeholder={t("workitems:tagsPlaceholder")}
            />
          </label>
        </div>

        {item.type === "task" ? (
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("workitems:taskParent")}</span>
            <Select
              value={parentWorkItemId}
              onValueChange={setParentWorkItemId}
              options={[{ value: "", label: t("workitems:taskParentNone") }, ...parentOptions]}
              ariaLabel={t("workitems:taskParent")}
            />
          </label>
        ) : null}

        {item.type === "defect" ? (
          <>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("workitems:relatedRequirement")}</span>
              <Select
                value={relatedRequirementId}
                onValueChange={setRelatedRequirementId}
                options={[
                  { value: "", label: t("workitems:relatedRequirementNone") },
                  ...(workItems.data ?? [])
                    .filter((candidate) => candidate.type === "requirement")
                    .map((candidate) => ({
                      value: candidate.id,
                      label: candidate.title,
                    })),
                ]}
                ariaLabel={t("workitems:relatedRequirement")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("workitems:severity")}</span>
              <Select
                value={severity}
                onValueChange={setSeverity}
                options={[
                  { value: "fatal", label: t("workitems:severities.fatal") },
                  { value: "serious", label: t("workitems:severities.serious") },
                  { value: "normal", label: t("workitems:severities.normal") },
                  { value: "minor", label: t("workitems:severities.minor") },
                ]}
                ariaLabel={t("workitems:severity")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("workitems:reproSteps")}</span>
              <Textarea
                value={reproSteps}
                onChange={(event) => setReproSteps(event.target.value)}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("workitems:environment")}</span>
              <Input value={environment} onChange={(event) => setEnvironment(event.target.value)} />
            </label>
          </>
        ) : null}

        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:branchUrl")}</span>
          <Input value={branchUrl} onChange={(event) => setBranchUrl(event.target.value)} />
        </label>

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t("common:cancel")}
          </Button>
          <Button
            onClick={() => void handleSubmit()}
            disabled={updateWorkItem.isPending || title.trim() === ""}
          >
            {t("common:save")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
