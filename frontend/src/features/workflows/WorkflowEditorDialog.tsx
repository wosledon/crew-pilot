import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useAgents } from "@/features/agents/api";
import type { WorkflowStepBody } from "./api";
import { useCreateWorkflow, useUpdateWorkflow, useWorkflow } from "./api";

interface StepForm {
  key: string;
  name: string;
  agentId: string;
  requiredJob: string;
  instruction: string;
  approvalPolicy: string;
  maxRetries: string;
  inputContract: string;
  outputContract: string;
}

export interface WorkflowEditorDialogProps {
  projectId: string;
  workflowId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const jobKeys = ["product", "architect", "developer", "qa", "manager", "ops"];

const newStep = (): StepForm => ({
  key: crypto.randomUUID(),
  name: "",
  agentId: "",
  requiredJob: "",
  instruction: "",
  approvalPolicy: "auto",
  maxRetries: "1",
  inputContract: "",
  outputContract: "",
});

/** 工作流可视化编辑器：步骤列表 + Agent/岗位/审批策略/重试，支持增删与排序。 */
export function WorkflowEditorDialog({
  projectId,
  workflowId,
  open,
  onOpenChange,
}: WorkflowEditorDialogProps) {
  const { t } = useTranslation(["workflows", "agents", "common"]);
  const workflow = useWorkflow(workflowId ?? undefined);
  const agents = useAgents();
  const createWorkflow = useCreateWorkflow(projectId);
  const updateWorkflow = useUpdateWorkflow(projectId);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [steps, setSteps] = useState<StepForm[]>([newStep()]);
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    if (!open) {
      setInitialized(false);
      return;
    }
    if (workflowId === null) {
      setName("");
      setDescription("");
      setSteps([newStep()]);
      setInitialized(true);
      return;
    }
    if (workflow.data !== undefined && !initialized) {
      setName(workflow.data.name);
      setDescription(workflow.data.description ?? "");
      setSteps(
        workflow.data.steps
          .slice()
          .sort((a, b) => a.order - b.order)
          .map((step) => ({
            key: step.id,
            name: step.name,
            agentId: step.agentId ?? "",
            requiredJob: step.requiredJob ?? "",
            instruction: step.instruction ?? "",
            approvalPolicy: step.approvalPolicy,
            maxRetries: String(step.maxRetries),
            inputContract: step.inputContract ?? "",
            outputContract: step.outputContract ?? "",
          })),
      );
      setInitialized(true);
    }
  }, [open, workflowId, workflow.data, initialized]);

  const updateStep = (key: string, patch: Partial<StepForm>) =>
    setSteps((previous) =>
      previous.map((step) => (step.key === key ? { ...step, ...patch } : step)),
    );

  const moveStep = (index: number, delta: number) =>
    setSteps((previous) => {
      const target = index + delta;
      if (target < 0 || target >= previous.length) {
        return previous;
      }
      const next = [...previous];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });

  const removeStep = (key: string) =>
    setSteps((previous) => previous.filter((step) => step.key !== key));

  const handleSave = async () => {
    const validSteps = steps.filter((step) => step.name.trim() !== "");
    if (name.trim() === "" || validSteps.length === 0) {
      toast.error(t("workflows:invalidSteps"));
      return;
    }

    const payload = validSteps.map<WorkflowStepBody>((step, index) => ({
      name: step.name.trim(),
      order: index,
      agentId: step.agentId === "" ? undefined : step.agentId,
      requiredJob: step.requiredJob === "" ? undefined : step.requiredJob,
      instruction: step.instruction.trim() || undefined,
      approvalPolicy: step.approvalPolicy,
      maxRetries: Number(step.maxRetries) || 0,
      inputContract: step.inputContract.trim() || undefined,
      outputContract: step.outputContract.trim() || undefined,
    }));

    try {
      if (workflowId === null) {
        await createWorkflow.mutateAsync({
          name: name.trim(),
          description: description.trim() || undefined,
          steps: payload,
        });
        toast.success(t("workflows:created"));
      } else {
        await updateWorkflow.mutateAsync({
          id: workflowId,
          name: name.trim(),
          description: description.trim() || undefined,
          steps: payload,
        });
        toast.success(t("workflows:updated"));
      }
      onOpenChange(false);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={workflowId === null ? t("workflows:createCustomTitle") : t("workflows:editTitle")}
      closeLabel={t("common:cancel")}
      className="max-w-3xl"
    >
      {workflowId !== null && workflow.isPending ? (
        <LoadingState />
      ) : (
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("workflows:workflowName")}</span>
              <Input
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder={t("workflows:workflowNamePlaceholder")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("workflows:workflowDescription")}</span>
              <Input value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
          </div>

          <div className="max-h-[55vh] space-y-3 overflow-y-auto pr-1">
            {steps.map((step, index) => (
              <div key={step.key} className="rounded-lg border border-border p-3">
                <div className="mb-2 flex items-center justify-between">
                  <span className="text-sm font-medium">
                    {t("workflows:step")} {index + 1}
                  </span>
                  <div className="flex gap-1">
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={t("workflows:moveUp")}
                      disabled={index === 0}
                      onClick={() => moveStep(index, -1)}
                    >
                      <ArrowUp className="h-4 w-4" />
                    </Button>
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={t("workflows:moveDown")}
                      disabled={index === steps.length - 1}
                      onClick={() => moveStep(index, 1)}
                    >
                      <ArrowDown className="h-4 w-4" />
                    </Button>
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={t("workflows:removeStep")}
                      disabled={steps.length <= 1}
                      onClick={() => removeStep(step.key)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <label className="block space-y-1.5 text-sm">
                    <span className="font-medium">{t("workflows:stepName")}</span>
                    <Input
                      value={step.name}
                      onChange={(event) => updateStep(step.key, { name: event.target.value })}
                      placeholder={t("workflows:stepNamePlaceholder")}
                    />
                  </label>
                  <label className="block space-y-1.5 text-sm">
                    <span className="font-medium">{t("workflows:agent")}</span>
                    <Select
                      value={step.agentId}
                      onValueChange={(value) => updateStep(step.key, { agentId: value })}
                      options={[
                        { value: "", label: t("workflows:agentAuto") },
                        ...(agents.data ?? []).map((agent) => ({
                          value: agent.id,
                          label: agent.name,
                        })),
                      ]}
                      ariaLabel={t("workflows:agent")}
                    />
                  </label>
                  <label className="block space-y-1.5 text-sm">
                    <span className="font-medium">{t("workflows:requiredJob")}</span>
                    <Select
                      value={step.requiredJob}
                      onValueChange={(value) => updateStep(step.key, { requiredJob: value })}
                      options={[
                        { value: "", label: t("workflows:jobNone") },
                        ...jobKeys.map((job) => ({
                          value: job,
                          label: t(`agents:jobs.${job}`),
                        })),
                      ]}
                      ariaLabel={t("workflows:requiredJob")}
                    />
                  </label>
                  <label className="block space-y-1.5 text-sm">
                    <span className="font-medium">{t("workflows:policy")}</span>
                    <Select
                      value={step.approvalPolicy}
                      onValueChange={(value) => updateStep(step.key, { approvalPolicy: value })}
                      options={["auto", "blockersOnly", "always"].map((policy) => ({
                        value: policy,
                        label: t(`workflows:policies.${policy.toLowerCase()}`),
                      }))}
                      ariaLabel={t("workflows:policy")}
                    />
                  </label>
                  <label className="block space-y-1.5 text-sm">
                    <span className="font-medium">{t("workflows:maxRetries")}</span>
                    <Input
                      type="number"
                      min={0}
                      max={10}
                      value={step.maxRetries}
                      onChange={(event) => updateStep(step.key, { maxRetries: event.target.value })}
                    />
                  </label>
                </div>

                <label className="mt-3 block space-y-1.5 text-sm">
                  <span className="font-medium">{t("workflows:instruction")}</span>
                  <Textarea
                    value={step.instruction}
                    onChange={(event) => updateStep(step.key, { instruction: event.target.value })}
                    placeholder={t("workflows:instructionPlaceholder")}
                    className="min-h-16"
                  />
                </label>

                <div className="mt-3 grid gap-3 sm:grid-cols-2">
                  <label className="block space-y-1.5 text-sm">
                    <span className="font-medium">{t("workflows:inputContract")}</span>
                    <Input
                      value={step.inputContract}
                      onChange={(event) =>
                        updateStep(step.key, { inputContract: event.target.value })
                      }
                    />
                  </label>
                  <label className="block space-y-1.5 text-sm">
                    <span className="font-medium">{t("workflows:outputContract")}</span>
                    <Input
                      value={step.outputContract}
                      onChange={(event) =>
                        updateStep(step.key, { outputContract: event.target.value })
                      }
                    />
                  </label>
                </div>
              </div>
            ))}
          </div>

          <div className="flex items-center justify-between">
            <Button
              variant="outline"
              onClick={() => setSteps((previous) => [...previous, newStep()])}
            >
              <Plus className="h-4 w-4" />
              {t("workflows:addStep")}
            </Button>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                {t("common:cancel")}
              </Button>
              <Button
                onClick={() => void handleSave()}
                disabled={createWorkflow.isPending || updateWorkflow.isPending}
              >
                {t("workflows:saveWorkflow")}
              </Button>
            </div>
          </div>
        </div>
      )}
    </Dialog>
  );
}
