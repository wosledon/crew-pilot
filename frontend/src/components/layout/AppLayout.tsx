import { useEffect, useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  FolderKanban,
  LayoutTemplate,
  Plug,
  Bot,
  Moon,
  Sun,
  Sparkles,
  Cable,
  ScrollText,
  LogOut,
  Gauge,
} from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Select";
import { Tooltip } from "@/components/ui/Tooltip";
import { supportedLanguages } from "@/i18n";
import { useRealtimeUpdates } from "@/lib/realtime";
import { useAuthStatus, useLogout } from "@/features/auth/api";
import { cn } from "@/lib/cn";

const navItems = [
  { to: "/dashboard", icon: Gauge, labelKey: "layout:nav.dashboard" },
  { to: "/projects", icon: FolderKanban, labelKey: "layout:nav.projects" },
  { to: "/prototypes", icon: LayoutTemplate, labelKey: "layout:nav.prototype" },
  { to: "/providers", icon: Plug, labelKey: "layout:nav.providers" },
  { to: "/agents", icon: Bot, labelKey: "layout:nav.agents" },
  { to: "/skills", icon: Sparkles, labelKey: "layout:nav.skills" },
  { to: "/mcp", icon: Cable, labelKey: "layout:nav.mcp" },
  { to: "/audit", icon: ScrollText, labelKey: "layout:nav.audit" },
];

/** 应用骨架：左侧固定导航 + 主内容区（卡片风格见 tokens.css） */
export function AppLayout() {
  const { t, i18n } = useTranslation();
  const [dark, setDark] = useState(() => window.matchMedia("(prefers-color-scheme: dark)").matches);
  const auth = useAuthStatus();
  const logout = useLogout();

  // 实时事件 → TanStack Query 缓存刷新
  useRealtimeUpdates();

  useEffect(() => {
    document.documentElement.classList.toggle("dark", dark);
  }, [dark]);

  const currentLanguage = i18n.language.startsWith("zh") ? "zh-CN" : "en";

  return (
    <div className="flex min-h-screen bg-surface">
      <aside className="fixed inset-y-0 left-0 flex w-64 flex-col bg-surface-container-low px-3 py-3">
        <div className="flex h-14 items-center gap-3 px-3">
          <span
            aria-hidden="true"
            className="grid h-10 w-10 place-items-center rounded-full bg-primary-container text-lg text-on-primary-container"
          >
            🧭
          </span>
          <div className="leading-tight">
            <div className="text-sm font-medium">{t("layout:sidebar.title")}</div>
            <div className="text-xs text-on-surface-variant">{t("layout:sidebar.subtitle")}</div>
          </div>
        </div>
        <nav className="mt-2 flex flex-col gap-0.5">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                cn(
                  "flex h-12 items-center gap-3 rounded-full px-4 text-sm transition-colors duration-200",
                  isActive
                    ? "bg-secondary-container font-medium text-on-secondary-container"
                    : "text-on-surface-variant hover:bg-on-surface/10",
                )
              }
            >
              <item.icon className="h-5 w-5" />
              {t(item.labelKey)}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="ml-64 flex min-h-screen flex-1 flex-col bg-surface">
        <header className="sticky top-0 z-30 flex h-16 items-center justify-between bg-surface/80 px-6 backdrop-blur">
          <span className="text-lg font-normal tracking-[0.15px]">{t("common:appName")}</span>
          <div className="flex items-center gap-2">
            {auth.data?.enabled ? (
              <Tooltip content={t("auth:logout")}>
                <Button
                  variant="outline"
                  size="icon"
                  aria-label={t("auth:logout")}
                  onClick={() => void logout.mutateAsync()}
                >
                  <LogOut className="h-4 w-4" />
                </Button>
              </Tooltip>
            ) : null}
            <Select
              value={currentLanguage}
              onValueChange={(value) => void i18n.changeLanguage(value)}
              options={supportedLanguages}
              ariaLabel={t("common:language")}
              className="w-36"
            />
            <Tooltip content={t("layout:theme.toggle")}>
              <Button
                variant="outline"
                size="icon"
                aria-label={t("layout:theme.toggle")}
                onClick={() => setDark((value) => !value)}
              >
                {dark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
              </Button>
            </Tooltip>
          </div>
        </header>

        <main className="flex-1 p-6 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
