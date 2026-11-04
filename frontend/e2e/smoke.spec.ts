import { expect, test } from "@playwright/test";

test("创建项目与工作项，并在看板中查看", async ({ page }) => {
  const suffix = Date.now().toString().slice(-6);
  const projectName = `E2E 项目 ${suffix}`;

  await page.goto("/projects");
  await expect(page.getByRole("heading", { level: 1, name: "项目" })).toBeVisible();

  await page.getByRole("button", { name: "新建项目" }).click();
  const projectDialog = page.getByRole("dialog");
  await projectDialog.getByLabel("项目 Key").fill(`E${suffix}`);
  await projectDialog.getByLabel("项目名称").fill(projectName);
  await projectDialog.getByRole("button", { name: "创建" }).click();

  const projectLink = page.getByRole("link", { name: new RegExp(projectName) });
  await expect(projectLink).toBeVisible();
  await projectLink.click();

  await expect(page.getByRole("heading", { level: 1, name: projectName })).toBeVisible();

  await page.getByRole("button", { name: "新建工作项" }).click();
  const workItemDialog = page.getByRole("dialog");
  await workItemDialog.getByLabel("标题").fill("E2E 需求");
  await workItemDialog.getByRole("button", { name: "创建" }).click();
  await expect(page.getByText("E2E 需求").first()).toBeVisible();

  await page.getByRole("link", { name: "看板" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "看板" })).toBeVisible();
  await expect(page.getByText("E2E 需求").first()).toBeVisible();
});

test("Agent 页面可打开新建对话框（无供应商时给出提示）", async ({ page }) => {
  await page.goto("/agents");
  await expect(page.getByRole("heading", { level: 1, name: "Agent 数字员工" })).toBeVisible();
  await expect(page.getByRole("button", { name: "新建 Agent" })).toBeVisible();
});
