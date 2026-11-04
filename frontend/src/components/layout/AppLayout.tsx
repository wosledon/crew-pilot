import { useEffect, useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { FolderKanban, LayoutTemplate, Plug, Bot, Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Select";
import { Tooltip } from "@/components/ui/Tooltip";
import { supportedLanguages } from "@/i18n";
import { cn } from "@/lib/cn";

const navItems = [
  { to: "/projects", icon: FolderKanban, labelKey: "layout:nav.projects" },
  { to: "/prototypes", icon: LayoutTemplate, labelKey: "layout:nav.prototype" },
  { to: "/providers", icon: Plug, labelKey: "layout:nav.providers" },
  { to: "/agents", icon: Bot, labelKey: "layout:nav.agents" },
];

/** 应用骨架：左侧固定导航 + 主内容区（卡片风格见 tokens.css） */
export function AppLayout() {
  const { t, i18n } = useTranslation();
  const [dark, setDark] = useState(() => window.matchMedia("(prefers-color-scheme: dark)").matches);

  useEffect(() => {
    document.documentElement.classList.toggle("dark", dark);
  }, [dark]);

  const currentLanguage = i18n.language.startsWith("zh") ? "zh-CN" : "en";

  return (
    <div className="flex min-h-screen">
      <aside className="fixed inset-y-0 left-0 flex w-56 flex-col border-r border-border bg-card">
        <div className="flex h-14 items-center gap-2 border-b border-border px-4">
          <span aria-hidden="true" className="text-lg">
            🧭
          </span>
          <div className="leading-tight">
            <div className="text-sm font-semibold">{t("layout:sidebar.title")}</div>
            <div className="text-xs text-muted-foreground">{t("layout:sidebar.subtitle")}</div>
          </div>
        </div>
        <nav className="flex flex-col gap-1 p-2">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors",
                  isActive
                    ? "bg-accent font-medium text-accent-foreground"
                    : "text-muted-foreground hover:bg-muted",
                )
              }
            >
              <item.icon className="h-4 w-4" />
              {t(item.labelKey)}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="ml-56 flex min-h-screen flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b border-border bg-card/80 px-6 backdrop-blur">
          <span className="text-sm text-muted-foreground">{t("common:appName")}</span>
          <div className="flex items-center gap-2">
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

        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
