import { useTranslation } from "react-i18next";
import { NavLink } from "react-router-dom";
import { cn } from "@/lib/cn";

export interface ProjectSubNavProps {
  projectId: string;
}

/** 项目内子导航：工作项 / 看板 / 迭代 / 甘特图 / 卡点队列 */
export function ProjectSubNav({ projectId }: ProjectSubNavProps) {
  const { t } = useTranslation(["projects", "kanban", "iterations", "gantt", "workflows"]);

  const tabs = [
    { to: `/projects/${projectId}`, label: t("projects:detail.workItems") },
    { to: `/projects/${projectId}/board`, label: t("kanban:title") },
    { to: `/projects/${projectId}/iterations`, label: t("iterations:title") },
    { to: `/projects/${projectId}/gantt`, label: t("gantt:title") },
    { to: `/projects/${projectId}/blockers`, label: t("workflows:blockerQueue") },
  ];

  return (
    <nav className="flex gap-1 border-b border-border">
      {tabs.map((tab) => (
        <NavLink
          key={tab.to}
          to={tab.to}
          end
          className={({ isActive }) =>
            cn(
              "-mb-px border-b-2 px-3 py-2 text-sm transition-colors",
              isActive
                ? "border-primary font-medium text-foreground"
                : "border-transparent text-muted-foreground hover:text-foreground",
            )
          }
        >
          {tab.label}
        </NavLink>
      ))}
    </nav>
  );
}
