import { expect, test as setup } from "@playwright/test";
import { required } from "./env";

// Signs in to the Admin Shell once; the admin projects reuse the session.
setup("sign in to the Admin Shell", async ({ page }) => {
  await page.goto("/apps/admin-shell/");
  await page.locator('input[name="username"]').fill(required("E2E_ADMIN_USER"));
  await page.locator('input[name="password"]').fill(required("E2E_ADMIN_PASSWORD"));
  await page.locator('button[type="submit"]').click();
  await expect(page.locator(".as-rail, .as-mbar").first()).toBeVisible();
  await page.context().storageState({ path: ".auth/admin.json" });
});
