import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { Button } from "@/components/ui/Button";

export interface CreateWorkItemDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (body: {
    type: string;
    title: string;
    description?: string;
    acceptanceCriteria?: string;
    priority?: string;
    estimatedHours?: number;
    assigneeName?: string;
    severity?: string;
    reproSteps?: string;
    environment?: string;
    tags?: string;
    iterationId?: string;
    dueDate?: string;
    parentWorkItemId?: string;
  }) => Promise<unknown>;
}

export function CreateWorkItemDialog({ open, onOpenChange, onSubmit }: CreateWorkItemDialogProps) {
  const { t } = useTranslation(["workitems", "common"]);
  const [type, setType] = useState("requirement");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [acceptanceCriteria, setAcceptanceCriteria] = useState("");
  const [priority, setPriority] = useState("p2");
  const [estimatedHours, setEstimatedHours] = useState("");
  const [assigneeName, setAssigneeName] = useState("");
  const [severity, setSeverity] = useState("normal");
  const [reproSteps, setReproSteps] = useState("");
  const [environment, setEnvironment] = useState("");
  const [tags, setTags] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const reset = () => {
    setTitle("");
    setDescription("");
    setAcceptanceCriteria("");
    setEstimatedHours("");
    setAssigneeName("");
    setReproSteps("");
    setEnvironment("");
    setTags("");
  };

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      await onSubmit({
        type,
        title: title.trim(),
        description: description.trim() || undefined,
        acceptanceCriteria: acceptanceCriteria.trim() || undefined,
        priority,
        estimatedHours: estimatedHours === "" ? undefined : Number(estimatedHours),
        assigneeName: assigneeName.trim() || undefined,
        severity: type === "defect" ? severity : undefined,
        reproSteps: type === "defect" ? reproSteps.trim() || undefined : undefined,
        environment: type === "defect" ? environment.trim() || undefined : undefined,
        tags: tags.trim() || undefined,
      });
      reset();
      onOpenChange(false);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("workitems:createTitle")}
      closeLabel={t("common:cancel")}
    >
      <div className="space-y-4">
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:type")}</span>
          <Select
            value={type}
            onValueChange={setType}
            options={[
              { value: "requirement", label: t("workitems:types.requirement") },
              { value: "defect", label: t("workitems:types.defect") },
              { value: "task", label: t("workitems:types.task") },
            ]}
            ariaLabel={t("workitems:type")}
          />
        </label>

        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:title")}</span>
          <Input
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            placeholder={t("workitems:titlePlaceholder")}
          />
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
            <span className="font-medium">{t("common:estimatedHours")}</span>
            <Input
              type="number"
              min={0}
              step={0.5}
              value={estimatedHours}
              onChange={(event) => setEstimatedHours(event.target.value)}
            />
          </label>
        </div>

        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:assignee")}</span>
          <Input
            value={assigneeName}
            onChange={(event) => setAssigneeName(event.target.value)}
            placeholder={t("workitems:assigneePlaceholder")}
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

        {type === "defect" ? (
          <>
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

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t("common:cancel")}
          </Button>
          <Button onClick={() => void handleSubmit()} disabled={submitting || title.trim() === ""}>
            {t("common:create")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
