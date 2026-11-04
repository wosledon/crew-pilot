import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import {
  Activity as ActivityIcon,
  AlertCircle,
  ArrowRight,
  Bot,
  CheckSquare,
  FolderKanban,
  Timer,
} from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { formatDateTime } from "@/lib/format";
import { priorityTone, statusTone } from "@/features/projects/statusFlow";
import { useDashboard } from "./api";

/** 驾驶舱：跨项目汇总卡点、待办、Agent 执行与项目进度（PRD 4.1 / 5.8） */
export function DashboardPage() {
  const { t } = useTranslation(["dashboard", "workitems", "collaboration", "common"]);
  const dashboard = useDashboard();

  if (dashboard.isPending) {
    return <LoadingState />;
  }
  if (dashboard.isError || dashboard.data === undefined) {
    return <ErrorState onRetry={() => void dashboard.refetch()} />;
  }

  const { stats, projects, blockers, todos, activeRuns, recentActivities } = dashboard.data;

  const statCards = [
    {
      key: "blockers",
      value: stats.openBlockerCount,
      icon: AlertCircle,
      to: "/projects",
      tone: stats.openBlockerCount > 0 ? "error" : "neutral",
    },
    { key: "runs", value: stats.activeRunCount, icon: Bot, to: "/projects", tone: "neutral" },
    { key: "todos", value: stats.todoCount, icon: CheckSquare, to: "/projects", tone: "neutral" },
    { key: "projects", value: stats.projectCount, icon: FolderKanban, to: "/projects", tone: "neutral" },
  ] as const;

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-normal tracking-tight">{t("dashboard:title")}</h1>
          <p className="mt-1 text-sm text-on-surface-variant">{t("dashboard:subtitle")}</p>
        </div>
        <span className="inline-flex items-center gap-2 rounded-full bg-surface-container-high px-4 py-2 text-xs text-on-surface-variant">
          <Timer className="h-4 w-4" aria-hidden="true" />
          {t("dashboard:weekHours")} · {stats.weekLoggedHours.toFixed(1)}h
        </span>
      </header>

      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4" aria-label={t("dashboard:statsLabel")}>
        {statCards.map((card) => {
          const Icon = card.icon;
          return (
            <Link
              key={card.key}
              to={card.to}
              className="group rounded-xl bg-surface-container p-4 transition-shadow hover:shadow-elevation-2"
            >
              <div className="flex items-center justify-between">
                <span className="text-sm text-on-surface-variant">
                  {t(`dashboard:stats.${card.key}`)}
                </span>
                <span
                  className={
                    card.tone === "error"
                      ? "rounded-full bg-error-container p-2 text-on-error-container"
                      : "rounded-full bg-secondary-container p-2 text-on-secondary-container"
                  }
                >
                  <Icon className="h-4 w-4" aria-hidden="true" />
                </span>
              </div>
              <p className="mt-3 text-3xl font-normal tracking-tight">{card.value}</p>
            </Link>
          );
        })}
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="flex-row items-center justify-between">
            <CardTitle className="flex items-center gap-2 text-base font-medium">
              <AlertCircle className="h-4 w-4 text-error" aria-hidden="true" />
              {t("dashboard:blockerQueue")}
            </CardTitle>
            <Badge tone={blockers.length > 0 ? "danger" : "neutral"}>{blockers.length}</Badge>
          </CardHeader>
          <CardContent>
            {blockers.length === 0 ? (
              <p className="py-6 text-center text-sm text-on-surface-variant">
                {t("dashboard:blockerEmpty")}
              </p>
            ) : (
              <ul className="space-y-2">
                {blockers.map((blocker) => (
                  <li key={blocker.id}>
                    <Link
                      to={`/projects/${blocker.projectId}/blockers`}
                      className="flex items-start justify-between gap-3 rounded-lg p-3 transition-colors hover:bg-surface-container-high"
                    >
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-medium">
                          {blocker.workItemTitle}
                        </span>
                        <span className="mt-0.5 block truncate text-xs text-on-surface-variant">
                          {blocker.projectName} · {blocker.reason}
                        </span>
                      </span>
                      <ArrowRight className="mt-0.5 h-4 w-4 shrink-0 text-on-surface-variant" />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex-row items-center justify-between">
            <CardTitle className="flex items-center gap-2 text-base font-medium">
              <Bot className="h-4 w-4 text-primary" aria-hidden="true" />
              {t("dashboard:activeRuns")}
            </CardTitle>
            <Badge tone={activeRuns.length > 0 ? "info" : "neutral"}>{activeRuns.length}</Badge>
          </CardHeader>
          <CardContent>
            {activeRuns.length === 0 ? (
              <p className="py-6 text-center text-sm text-on-surface-variant">
                {t("dashboard:runsEmpty")}
              </p>
            ) : (
              <ul className="space-y-2">
                {activeRuns.map((run) => (
                  <li
                    key={run.id}
                    className="flex items-start justify-between gap-3 rounded-lg p-3 hover:bg-surface-container-high"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-medium">
                        🤖 {run.agentName} · {run.workItemTitle ?? t("dashboard:noWorkItem")}
                      </span>
                      <span className="mt-0.5 block text-xs text-on-surface-variant">
                        {run.projectName ?? "—"} · {formatDateTime(run.createdAt)}
                      </span>
                    </span>
                    <Badge tone="info">{t("dashboard:running")}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base font-medium">
              <CheckSquare className="h-4 w-4 text-tertiary" aria-hidden="true" />
              {t("dashboard:myTodos")}
            </CardTitle>
          </CardHeader>
          <CardContent>
            {todos.length === 0 ? (
              <p className="py-6 text-center text-sm text-on-surface-variant">
                {t("dashboard:todosEmpty")}
              </p>
            ) : (
              <ul className="space-y-2">
                {todos.map((todo) => (
                  <li key={todo.workItemId}>
                    <Link
                      to={`/projects/${todo.projectId}`}
                      className="flex items-center justify-between gap-3 rounded-lg p-3 transition-colors hover:bg-surface-container-high"
                    >
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-medium">{todo.title}</span>
                        <span className="mt-0.5 block text-xs text-on-surface-variant">
                          {todo.projectName} · {t(`workitems:types.${todo.type}`)}
                          {todo.dueDate ? ` · ${todo.dueDate}` : ""}
                        </span>
                      </span>
                      <span className="flex shrink-0 items-center gap-1.5">
                        <Badge tone={priorityTone(todo.priority)}>
                          {t(`workitems:priorities.${todo.priority}`)}
                        </Badge>
                        <Badge tone={statusTone(todo.status)}>
                          {t(`workitems:statuses.${todo.status}`, { defaultValue: todo.status })}
                        </Badge>
                        {todo.overdue ? <Badge tone="danger">{t("dashboard:overdue")}</Badge> : null}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base font-medium">
              <FolderKanban className="h-4 w-4 text-secondary" aria-hidden="true" />
              {t("dashboard:projectProgress")}
            </CardTitle>
          </CardHeader>
          <CardContent>
            {projects.length === 0 ? (
              <p className="py-6 text-center text-sm text-on-surface-variant">
                {t("dashboard:noProjects")}
              </p>
            ) : (
              <ul className="space-y-4">
                {projects.map((project) => (
                  <li key={project.id}>
                    <Link to={`/projects/${project.id}`} className="group block">
                      <div className="flex items-baseline justify-between gap-2">
                        <span className="truncate text-sm font-medium">
                          <span className="text-on-surface-variant">{project.key}</span> · {project.name}
                        </span>
                        <span className="shrink-0 text-xs text-on-surface-variant">
                          {t("dashboard:progress", {
                            done: project.doneCount,
                            total: project.workItemCount,
                          })}
                        </span>
                      </div>
                      <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-secondary-container">
                        <div
                          className="h-full rounded-full bg-primary transition-[width]"
                          style={{ width: `${project.progress}%` }}
                        />
                      </div>
                      <p className="mt-1.5 text-xs text-on-surface-variant">
                        {project.activeIterationName
                          ? t("dashboard:iteration", {
                              name: project.activeIterationName,
                              date: project.activeIterationEndDate ?? "—",
                            })
                          : t("dashboard:noActiveIteration")}
                      </p>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base font-medium">
            <ActivityIcon className="h-4 w-4 text-primary" aria-hidden="true" />
            {t("dashboard:recentActivity")}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {recentActivities.length === 0 ? (
            <EmptyState>{t("dashboard:activityEmpty")}</EmptyState>
          ) : (
            <ul className="space-y-1">
              {recentActivities.map((event) => (
                <li
                  key={event.id}
                  className="flex items-center justify-between gap-3 rounded-lg px-3 py-2 text-sm hover:bg-surface-container-high"
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <Badge tone={event.actorKind === "agent" ? "info" : "neutral"}>
                      {t(`collaboration:authorKinds.${event.actorKind ?? "user"}`)}
                    </Badge>
                    <span className="truncate">
                      {t(`collaboration:events.${event.eventType}`, { defaultValue: event.eventType })}
                    </span>
                    <span className="hidden truncate text-xs text-on-surface-variant sm:inline">
                      {event.projectName ?? ""}
                    </span>
                  </span>
                  <span className="shrink-0 text-xs text-on-surface-variant">
                    {formatDateTime(event.createdAt)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
