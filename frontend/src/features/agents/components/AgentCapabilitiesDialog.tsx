import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useSkills, useMcpServers } from "@/features/platform/api";
import { useAgent, useUpdateAgent } from "../api";

export interface AgentCapabilitiesDialogProps {
  agentId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Agent 能力装配：选择 Skills 与授权的 MCP Server。 */
export function AgentCapabilitiesDialog({
  agentId,
  open,
  onOpenChange,
}: AgentCapabilitiesDialogProps) {
  const { t } = useTranslation(["agents", "skills", "mcp", "common"]);
  const agent = useAgent(agentId);
  const skills = useSkills();
  const mcpServers = useMcpServers();
  const updateAgent = useUpdateAgent(agentId);

  const [skillIds, setSkillIds] = useState<string[]>([]);
  const [mcpServerIds, setMcpServerIds] = useState<string[]>([]);
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    if (agent.data !== undefined && !initialized) {
      setSkillIds(agent.data.skillIds);
      setMcpServerIds(agent.data.mcpServerIds);
      setInitialized(true);
    }
  }, [agent.data, initialized]);

  const toggle = (values: string[], id: string) =>
    values.includes(id) ? values.filter((value) => value !== id) : [...values, id];

  const handleSave = async () => {
    if (agent.data === undefined) {
      return;
    }
    try {
      await updateAgent.mutateAsync({
        name: agent.data.name,
        job: agent.data.job,
        systemPrompt: agent.data.systemPrompt,
        tools: agent.data.tools,
        commandWhitelist: agent.data.commandWhitelist,
        enabled: agent.data.enabled,
        skillIds,
        mcpServerIds,
      });
      toast.success(t("agents:updated"));
      onOpenChange(false);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("agents:capabilitiesTitle")}
      closeLabel={t("common:cancel")}
      className="max-w-2xl"
    >
      {agent.isPending ? (
        <LoadingState />
      ) : (
        <div className="space-y-5">
          <section>
            <h4 className="text-sm font-medium">{t("skills:title")}</h4>
            {(skills.data ?? []).length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">{t("skills:empty")}</p>
            ) : (
              <div className="mt-2 flex flex-wrap gap-2">
                {(skills.data ?? []).map((skill) => (
                  <Button
                    key={skill.id}
                    size="sm"
                    variant={skillIds.includes(skill.id) ? "primary" : "outline"}
                    onClick={() => setSkillIds(toggle(skillIds, skill.id))}
                  >
                    {skill.name}
                  </Button>
                ))}
              </div>
            )}
          </section>

          <section>
            <h4 className="text-sm font-medium">{t("mcp:title")}</h4>
            {(mcpServers.data ?? []).length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">{t("mcp:empty")}</p>
            ) : (
              <div className="mt-2 flex flex-wrap gap-2">
                {(mcpServers.data ?? []).map((server) => (
                  <Button
                    key={server.id}
                    size="sm"
                    variant={mcpServerIds.includes(server.id) ? "primary" : "outline"}
                    onClick={() => setMcpServerIds(toggle(mcpServerIds, server.id))}
                  >
                    {server.name}
                    <Badge tone="neutral">{server.transport}</Badge>
                  </Button>
                ))}
              </div>
            )}
          </section>

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common:cancel")}
            </Button>
            <Button onClick={() => void handleSave()} disabled={updateAgent.isPending}>
              {t("common:save")}
            </Button>
          </div>
        </div>
      )}
    </Dialog>
  );
}
