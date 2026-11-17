import { expect, test as setup } from "@playwright/test";
import { required } from "./env";

// Signs the buyer in to the storefront once; the storefront projects reuse the session.
setup("sign in to the storefront", async ({ page }) => {
  setup.setTimeout(180_000);
  await page.goto("/sign-in");
  const email = page.locator('input[autocomplete~="email"]');
  // a dev server compiles the app on a fresh browser's first visit
  await expect(email).toBeVisible({ timeout: 120_000 });
  await email.fill(required("E2E_BUYER_USER"));
  // a store with passwordless sign-in asks for the email only (a code is sent); the password form is one link away
  const password = page.locator('input[autocomplete="current-password"]');
  const passwordInstead = page.getByText("Sign in with a password instead");
  await expect(password.or(passwordInstead).first()).toBeVisible();
  if (!(await password.isVisible())) {
    await passwordInstead.click();
    if ((await email.inputValue()) === "") await email.fill(required("E2E_BUYER_USER"));
  }
  await password.fill(required("E2E_BUYER_PASSWORD"));
  await page.getByTestId("login-button").click();
  await expect(page).not.toHaveURL(/sign-in/);
  await page.context().storageState({ path: ".auth/buyer.json" });
});
