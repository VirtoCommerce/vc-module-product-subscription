import { expect, test, type Page } from "@playwright/test";

// Subscribe & Save in vc-frontend, on a desktop and on a phone, as a signed-in buyer with active subscriptions.
// The pause scenario resumes the subscription it paused.

test.describe.configure({ mode: "serial" });

// a dev server may still be compiling the page on its first visit from a new browser (a blank page until then)
async function open(page: Page, path: string) {
  await page.goto(path);
  await expect(page.locator("main").first()).toBeVisible({ timeout: 120_000 });
}

test("the product page offers Subscribe & Save with a frequency", async ({ page }) => {
  test.setTimeout(180_000);
  await open(page, "/search?q=pepsi");
  // the product's name, not its image (whose overlay takes the click)
  await page.getByRole("link").filter({ hasText: /PEPSI COLA REGULAR CRATE/i }).first().click();
  const subscribe = page.getByTestId("purchase-option-subscribe");
  await expect(subscribe).toBeVisible();
  await expect(page.getByTestId("purchase-option-once")).toBeVisible();
  await subscribe.click();
  await expect(page.getByTestId("purchase-option-frequency")).toBeVisible();
});

test("the account lists subscriptions, deliveries and status tabs", async ({ page }) => {
  await open(page, "/account/subscriptions");
  await expect(page.locator('[data-test-id^="subscription-row-"]').first()).toBeVisible();
  await expect(page.locator('[data-test-id^="subscriptions-tab-"]').first()).toBeVisible();
  await expect(page.getByText("Upcoming deliveries")).toBeVisible();
});

test("a subscription shows its schedule and is paused and resumed", async ({ page }) => {
  await open(page, "/account/subscriptions");
  await page.locator('[data-test-id^="subscription-row-"]', { hasText: "Active" }).first().click();
  await expect(page).toHaveURL(/\/account\/subscriptions\/.+/);
  await expect(page.getByTestId("subscription-quantity")).toBeVisible();
  await expect(page.getByTestId("subscription-frequency")).toBeVisible();
  await expect(page.getByTestId("subscription-next-date")).toBeVisible();

  await page.getByTestId("subscription-pause").click();
  await page.getByTestId("subscription-pause-confirm").click();
  const resume = page.getByTestId("subscription-resume");
  await expect(resume).toBeVisible();
  await resume.click();
  await expect(page.getByTestId("subscription-pause")).toBeVisible();
});

test("saved payment methods are listed", async ({ page }) => {
  await open(page, "/account/payment-methods");
  await expect(page.locator('[data-test-id^="saved-card-"]').first()).toBeVisible();
  await expect(page.getByTestId("saved-card-add")).toBeVisible();
});
