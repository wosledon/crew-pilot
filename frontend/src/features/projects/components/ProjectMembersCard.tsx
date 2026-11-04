import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Select } from "@/components/ui/Select";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useAgents } from "@/features/agents/api";
import type { ProjectMember } from "@/lib/types";
import {
  useAddProjectMember,
  useDeleteProjectMember,
  useProjectMembers,
  useUpdateProjectMember,
} from "../api";

const roleKeys = [
  "owner",
  "product",
  "architect",
  "developer",
  "qa",
  "manager",
  "ops",
  "admin",
] as const;

export interface ProjectMembersCardProps {
  projectId: string;
}

/** 项目成员：Owner + Agent 数字员工，支持添加、调整角色、移除。 */
export function ProjectMembersCard({ projectId }: ProjectMembersCardProps) {
  const { t } = useTranslation(["projects", "agents", "common", "collaboration"]);
  const members = useProjectMembers(projectId);
  const agents = useAgents();
  const addMember = useAddProjectMember(projectId);
  const updateMember = useUpdateProjectMember(projectId);
  const removeMember = useDeleteProjectMember(projectId);
  const confirm = useConfirm();
  const [agentId, setAgentId] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const roleOptions = roleKeys.map((role) => ({
    value: role,
    label: t(`projects:roles.${role}`),
  }));

  const memberNames = new Set((members.data ?? []).map((member) => member.displayName));
  const availableAgents = (agents.data ?? []).filter(
    (agent) => agent.enabled && !memberNames.has(agent.name),
  );

  const handleAdd = async () => {
    if (agentId === "") {
      return;
    }
    try {
      await addMember.mutateAsync({ agentId });
      toast.success(t("projects:memberAdded"));
      setAgentId("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleRoleChange = async (member: ProjectMember, role: string) => {
    try {
      await updateMember.mutateAsync({
        id: member.id,
        displayName: member.displayName,
        role,
      });
      toast.success(t("projects:memberUpdated"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleRemove = async (member: ProjectMember) => {
    const approved = await confirm({
      title: t("projects:removeMemberConfirm"),
      description: t("projects:removeMemberConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await removeMember.mutateAsync(member.id);
      toast.success(t("projects:memberRemoved"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("projects:members")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {members.isPending ? (
          <LoadingState />
        ) : members.isError ? (
          <ErrorState onRetry={() => void members.refetch()} />
        ) : (members.data ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("projects:noMembers")}</p>
        ) : (
          <ul className="space-y-2">
            {(members.data ?? []).map((member) => (
              <li
                key={member.id}
                className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2"
              >
                <div className="flex min-w-0 items-center gap-2">
                  <Badge tone={member.kind === "agent" ? "info" : "neutral"}>
                    {member.kind === "agent" ? "Agent" : t("collaboration:authorKinds.user")}
                  </Badge>
                  <span className="truncate text-sm font-medium">{member.displayName}</span>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <Select
                    value={member.role}
                    onValueChange={(role) => void handleRoleChange(member, role)}
                    options={roleOptions}
                    ariaLabel={t("projects:memberRole")}
                    className="w-36"
                    disabled={member.role === "owner"}
                  />
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={t("common:delete")}
                    disabled={member.role === "owner"}
                    onClick={() => void handleRemove(member)}
                  >
                    <Trash2 className="h-3.5 w-3.5 text-destructive" />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}

        <div className="flex items-center gap-2 border-t border-border pt-3">
          <Select
            value={agentId}
            onValueChange={setAgentId}
            options={availableAgents.map((agent) => ({
              value: agent.id,
              label: `${agent.name}（${t(`agents:jobs.${agent.job}`)}）`,
            }))}
            placeholder={t("projects:agentMember")}
            ariaLabel={t("projects:agentMember")}
            className="flex-1"
          />
          <Button onClick={() => void handleAdd()} disabled={agentId === "" || addMember.isPending}>
            <Plus className="h-4 w-4" />
            {t("projects:addMember")}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
