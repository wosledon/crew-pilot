import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Star, Trash2, Users } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { EmptyState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useSetDefaultTeam } from "@/features/projects/api";
import { useAgentTeams, useAgents, useCreateAgentTeam, useDeleteAgentTeam } from "../api";

export interface AgentTeamCardProps {
  projectId: string;
  defaultTeamId: string | null;
}

/** 项目 Agent 团队：建队 / 成员装配 / 设为默认团队（工作流按岗位解析 Agent）。 */
export function AgentTeamCard({ projectId, defaultTeamId }: AgentTeamCardProps) {
  const { t } = useTranslation(["agents", "common"]);
  const teams = useAgentTeams(projectId);
  const agents = useAgents();
  const createTeam = useCreateAgentTeam(projectId);
  const deleteTeam = useDeleteAgentTeam(projectId);
  const setDefault = useSetDefaultTeam(projectId);
  const confirm = useConfirm();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState("");
  const [goal, setGoal] = useState("");
  const [selectedAgentIds, setSelectedAgentIds] = useState<string[]>([]);

  const handleCreate = async () => {
    try {
      await createTeam.mutateAsync({
        name: name.trim(),
        goal: goal.trim() || undefined,
        agentIds: selectedAgentIds,
      });
      toast.success(t("agents:teamCreated"));
      setDialogOpen(false);
      setName("");
      setGoal("");
      setSelectedAgentIds([]);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  const handleDelete = async (id: string) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: t("agents:teamDeleteConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteTeam.mutateAsync(id);
      toast.success(t("agents:teamDeleted"));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  const handleSetDefault = async (teamId: string | null) => {
    try {
      await setDefault.mutateAsync(teamId);
      toast.success(t("agents:defaultTeamSet"));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="flex items-center gap-2">
          <Users className="h-4 w-4" />
          {t("agents:teamsTitle")}
        </CardTitle>
        <Button size="sm" variant="outline" onClick={() => setDialogOpen(true)}>
          {t("agents:createTeam")}
        </Button>
      </CardHeader>
      <CardContent className="space-y-3">
        {teams.isPending ? (
          <LoadingState />
        ) : (teams.data ?? []).length === 0 ? (
          <EmptyState>{t("agents:emptyTeams")}</EmptyState>
        ) : (
          <ul className="space-y-2">
            {(teams.data ?? []).map((team) => {
              const isDefault = team.id === defaultTeamId;
              return (
                <li key={team.id} className="rounded-md border border-border px-3 py-2">
                  <div className="flex items-center justify-between gap-2">
                    <span className="flex min-w-0 items-center gap-2">
                      <span className="truncate text-sm font-medium">{team.name}</span>
                      {isDefault ? <Badge tone="primary">{t("agents:defaultTeam")}</Badge> : null}
                    </span>
                    <span className="flex shrink-0 items-center gap-1">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => void handleSetDefault(isDefault ? null : team.id)}
                        disabled={setDefault.isPending}
                      >
                        <Star className="h-3.5 w-3.5" />
                        {isDefault ? t("agents:clearDefaultTeam") : t("agents:setDefaultTeam")}
                      </Button>
                      <Button
                        size="icon"
                        variant="ghost"
                        aria-label={t("common:delete")}
                        onClick={() => void handleDelete(team.id)}
                      >
                        <Trash2 className="h-3.5 w-3.5 text-destructive" />
                      </Button>
                    </span>
                  </div>
                  {team.goal ? (
                    <p className="mt-0.5 truncate text-xs text-muted-foreground">{team.goal}</p>
                  ) : null}
                  <p className="mt-1 text-xs text-muted-foreground">
                    {team.members.length === 0
                      ? t("agents:teamEmptyMembers")
                      : team.members
                          .map(
                            (member) =>
                              `${member.agentName}${member.role ? `(${member.role})` : ""}`,
                          )
                          .join(" · ")}
                  </p>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>

      <Dialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        title={t("agents:createTeam")}
        closeLabel={t("common:cancel")}
        className="max-w-lg"
      >
        <div className="space-y-3">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("agents:teamName")}</span>
            <Input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={t("agents:teamNamePlaceholder")}
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("agents:teamGoal")}</span>
            <Input
              value={goal}
              onChange={(event) => setGoal(event.target.value)}
              placeholder={t("agents:teamGoalPlaceholder")}
            />
          </label>
          <div className="space-y-2">
            <span className="text-sm font-medium">{t("agents:teamMembers")}</span>
            <div className="flex flex-wrap gap-2">
              {(agents.data ?? []).map((agent) => (
                <Button
                  key={agent.id}
                  size="sm"
                  variant={selectedAgentIds.includes(agent.id) ? "primary" : "outline"}
                  onClick={() =>
                    setSelectedAgentIds((previous) =>
                      previous.includes(agent.id)
                        ? previous.filter((id) => id !== agent.id)
                        : [...previous, agent.id],
                    )
                  }
                >
                  {agent.name}
                </Button>
              ))}
              {(agents.data ?? []).length === 0 ? (
                <p className="text-sm text-muted-foreground">{t("agents:empty")}</p>
              ) : null}
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              {t("common:cancel")}
            </Button>
            <Button
              onClick={() => void handleCreate()}
              disabled={name.trim() === "" || createTeam.isPending}
            >
              {t("common:create")}
            </Button>
          </div>
        </div>
      </Dialog>
    </Card>
  );
}
