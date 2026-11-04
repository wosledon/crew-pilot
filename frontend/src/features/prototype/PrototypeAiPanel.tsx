import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { Loader2, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { toast } from "@/components/ui/Toast";
import { useAgentRun, useAgents } from "@/features/agents/api";
import { useGeneratePrototype } from "./api";

export interface PrototypeAiPanelProps {
  projectId: string;
  /** 当前选中的原型：存在时让 Agent 在其基础上迭代 */
  currentPrototypeId: string | null;
  onGenerated: (prototypeId: string) => void | Promise<void>;
}

/** 原型工坊主路径：产品 Agent 依据自然语言描述产出/迭代 HTML 原型。 */
export function PrototypeAiPanel({
  projectId,
  currentPrototypeId,
  onGenerated,
}: PrototypeAiPanelProps) {
  const { t } = useTranslation(["prototype", "common"]);
  const agents = useAgents();
  const generate = useGeneratePrototype(projectId);
  const [agentId, setAgentId] = useState("");
  const [brief, setBrief] = useState("");
  const [runId, setRunId] = useState<string | undefined>(undefined);
  const [targetPrototypeId, setTargetPrototypeId] = useState<string | null>(null);
  const run = useAgentRun(runId);

  const productAgents = useMemo(
    () => (agents.data ?? []).filter((agent) => agent.enabled && agent.job === "product"),
    [agents.data],
  );

  useEffect(() => {
    if (agentId === "" && productAgents.length > 0) {
      setAgentId(productAgents[0].id);
    }
  }, [agentId, productAgents]);

  const status = run.data?.status;
  useEffect(() => {
    if (status === "succeeded" && targetPrototypeId !== null) {
      toast.success(t("prototype:ai.succeeded"));
      void onGenerated(targetPrototypeId);
      setRunId(undefined);
      setTargetPrototypeId(null);
      setBrief("");
    } else if (status === "failed" || status === "canceled") {
      toast.error(run.data?.error ?? t("prototype:ai.failed"));
      setRunId(undefined);
      setTargetPrototypeId(null);
    }
  }, [status, targetPrototypeId, run.data?.error, onGenerated, t]);

  const running = generate.isPending || status === "queued" || status === "running";

  const handleGenerate = async () => {
    if (brief.trim() === "" || agentId === "") {
      return;
    }
    try {
      const result = await generate.mutateAsync({
        brief: brief.trim(),
        agentId,
        prototypeId: currentPrototypeId ?? undefined,
      });
      setTargetPrototypeId(result.prototypeId);
      setRunId(result.runId);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Sparkles className="h-4 w-4 text-primary" aria-hidden="true" />
          {t("prototype:ai.title")}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-xs text-on-surface-variant">{t("prototype:ai.hint")}</p>
        {productAgents.length === 0 ? (
          <p className="rounded-lg bg-warning-container px-3 py-2 text-xs text-on-warning-container">
            {t("prototype:ai.noAgent")}{" "}
            <Link to="/agents" className="font-medium underline">
              {t("prototype:ai.createAgent")}
            </Link>
          </p>
        ) : (
          <>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("prototype:ai.agent")}</span>
              <Select
                value={agentId}
                onValueChange={setAgentId}
                options={productAgents.map((agent) => ({
                  value: agent.id,
                  label: agent.name,
                }))}
                ariaLabel={t("prototype:ai.agent")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("prototype:ai.brief")}</span>
              <Textarea
                value={brief}
                onChange={(event) => setBrief(event.target.value)}
                placeholder={t("prototype:ai.briefPlaceholder")}
                rows={5}
              />
            </label>
            <p className="text-xs text-on-surface-variant">
              {currentPrototypeId === null
                ? t("prototype:ai.newHint")
                : t("prototype:ai.iterateHint")}
            </p>
            <Button
              className="w-full"
              onClick={() => void handleGenerate()}
              disabled={running || brief.trim() === "" || agentId === ""}
            >
              {running ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  {t("prototype:ai.running")}
                </>
              ) : (
                <>
                  <Sparkles className="h-4 w-4" />
                  {t("prototype:ai.generate")}
                </>
              )}
            </Button>
            {running ? (
              <p className="text-xs text-on-surface-variant">
                {t("prototype:ai.runningHint", { agent: run.data?.agentName ?? "" })}
              </p>
            ) : null}
          </>
        )}
      </CardContent>
    </Card>
  );
}
