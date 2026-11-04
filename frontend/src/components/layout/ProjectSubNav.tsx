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
    <nav className="flex flex-wrap gap-1 rounded-full bg-surface-container-high p-1">
      {tabs.map((tab) => (
        <NavLink
          key={tab.to}
          to={tab.to}
          end
          className={({ isActive }) =>
            cn(
              "rounded-full px-4 py-2 text-sm transition-colors duration-200",
              isActive
                ? "bg-secondary-container font-medium text-on-secondary-container"
                : "text-on-surface-variant hover:bg-on-surface/10",
            )
          }
        >
          {tab.label}
        </NavLink>
      ))}
    </nav>
  );
}
