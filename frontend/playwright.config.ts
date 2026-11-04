import { defineConfig, devices } from "@playwright/test";

/**
 * E2E：启动 API（http profile，口径 5080）与 Vite dev server（5173，代理 /api）。
 * 数据库使用 e2e-crewpilot.db（开发 SQLite，已在 .gitignore 忽略）。
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [["list"]],
  use: {
    baseURL: "http://127.0.0.1:5173",
    locale: "zh-CN",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: "dotnet run --project ../src/CrewPilot.Api --launch-profile http",
      url: "http://127.0.0.1:5080/health",
      reuseExistingServer: true,
      timeout: 180_000,
      env: {
        Database__AutoMigrate: "true",
        ConnectionStrings__CrewPilot: "Data Source=e2e-crewpilot.db",
        Providers__RefreshHours: "0",
      },
    },
    {
      command: "npm run dev -- --host 127.0.0.1 --port 5173 --strictPort",
      url: "http://127.0.0.1:5173",
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
});
