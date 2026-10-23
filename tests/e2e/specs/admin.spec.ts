import { expect, test } from "@playwright/test";
import { command, confirm, isPhone, openRoute, topBlade, watchConsole } from "./shell";

// The Product Subscriptions back office in the Admin Shell, on a desktop and on a phone. Every scenario leaves the data
// as it found it: edits are reset, a paused subscription is resumed, the temporary plan is deleted.
// Needs at least one active subscription with renewals, and an active plan (the module's demo data has both).

const SUBSCRIPTION_NUMBER = /SUB\d{6}-\d{5}/;
const RENEWAL_NUMBER = /REN\d{6}-\d{5}/;

test.describe.configure({ mode: "serial" });

let consoleErrors: string[] = [];
test.beforeEach(({ page }) => {
  consoleErrors = watchConsole(page);
});
test.afterEach(() => {
  expect(consoleErrors, "console errors").toEqual([]);
});

test("the menu opens the subscriptions list", async ({ page }, testInfo) => {
  await page.goto("/apps/admin-shell/#/");
  if (isPhone(testInfo)) await page.locator(".as-mbar__menu").click();
  // no section of its own yet: under More until it is placed with the menu editor
  await page.locator(".as-rail__item", { hasText: "More" }).click();
  await page.locator(".as-item__main", { hasText: "Subscriptions" }).first().click();
  await expect(topBlade(page).getByText(SUBSCRIPTION_NUMBER).first()).toBeVisible();
});

test("subscriptions are listed and searchable", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  const blade = topBlade(page);
  const numbers = await blade.getByText(SUBSCRIPTION_NUMBER).allInnerTexts();
  expect(numbers.length).toBeGreaterThan(0);

  const number = numbers[numbers.length - 1].trim();
  await blade.getByPlaceholder("Search by number, product, SKU or customer").fill(number);
  await expect(blade.getByText(SUBSCRIPTION_NUMBER)).toHaveCount(1);
  await expect(blade.getByText(number)).toBeVisible();
});

test("a subscription's schedule is edited, validated by the server and reset", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  await topBlade(page).getByText(SUBSCRIPTION_NUMBER).first().click();
  const blade = topBlade(page);
  await expect(blade.getByText("Schedule")).toBeVisible();
  await expect(blade.getByText("Customer and delivery")).toBeVisible();

  const quantity = blade.locator('input[type="number"]').first();
  const original = await quantity.inputValue();
  await quantity.fill(String(Number(original) + 1));
  await expect(page.getByText("Has unsaved changes")).toBeVisible();

  // the module checks pack size and quantity limits: a rejected save shows the server's reason, the edit stays
  const saved = page.waitForResponse((r) => r.request().method() === "PUT" && r.url().includes("/api/product-subscription/subscriptions/"));
  await command(page, "Save");
  const response = await saved;
  if (!response.ok()) {
    const { message } = await response.json();
    await expect(page.getByText(message).first()).toBeVisible();
    // the framework logs the rejected call (the 400 and the failed action): expected here
    consoleErrors = consoleErrors.filter((e) => !e.includes("400") && !e.includes(message));
    await command(page, "Reset");
  } else {
    // accepted: put the old quantity back
    await quantity.fill(original);
    await command(page, "Save");
  }
  await expect(quantity).toHaveValue(original);
  await expect(page.getByText("Has unsaved changes")).toHaveCount(0);
});

test("a subscription is paused and resumed", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  // the list is ordered by next order date: the first subscription is the next one due, normally active
  await topBlade(page).getByText(SUBSCRIPTION_NUMBER).first().click();
  const blade = topBlade(page);
  await expect(blade.getByText("Schedule")).toBeVisible();
  const status = (await blade.locator(".ps-hero .vc-status").innerText()).trim();
  test.skip(status !== "Active", `the first subscription is ${status}, not Active`);

  await command(page, "Pause");
  await expect(blade.getByText("Pause subscription")).toBeVisible();
  await blade.locator(".vc-card", { hasText: "Pause subscription" }).getByRole("button", { name: "Pause", exact: true }).click();
  await expect(blade.locator(".ps-hero").getByText("Paused")).toBeVisible();

  await command(page, "Resume");
  await expect(blade.locator(".ps-hero").getByText("Active")).toBeVisible();
});

test("renewals are listed and a renewal shows its payment, items and history", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  await command(page, "Renewals");
  const list = topBlade(page);
  await expect(list.getByText(RENEWAL_NUMBER).first()).toBeVisible();

  // a renewal with a placed order
  await list.getByText(/CO\d{6}-\d{5}/).first().click();
  const blade = topBlade(page);
  await expect(blade.getByText("Order and payment")).toBeVisible();
  await expect(blade.getByText("Items")).toBeVisible();
  await expect(blade.getByText("History")).toBeVisible();
  await expect(blade.locator(".ps-timeline__item").first()).toBeVisible();

  // the order opens in the Orders module's screen (its AngularJS blade inside the shell without its Vue plugin)
  await blade.locator(".ps-info .ps-link").first().click();
  await expect(page).toHaveURL(/legacy\?url=\/workspace\/orders|orders/);
});

test("a subscription opens its renewal from its renewals card", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  await topBlade(page).getByText(SUBSCRIPTION_NUMBER).first().click();
  const subscription = topBlade(page);
  await subscription.locator(".ps-row").first().click();
  await expect(topBlade(page).getByText("Order and payment")).toBeVisible();
  await expect(topBlade(page).getByText(RENEWAL_NUMBER).first()).toBeVisible();
});

test("a plan's products are picked from the catalog, then the change is reset", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  await command(page, "Plans");
  await topBlade(page).locator(".ps-product-cell__name").first().click();
  const plan = topBlade(page);
  await expect(plan.getByText("Delivery frequencies")).toBeVisible();
  const chips = plan.locator(".ps-chip");
  const before = await chips.count();

  await plan.getByRole("button", { name: "Add products or categories" }).click();
  const picker = topBlade(page);
  await expect(picker.getByText("Type to search products and categories of every catalog.")).toBeVisible();
  await picker.getByPlaceholder("Search products and categories").fill("pepsi");
  const result = picker.getByText(/PEPSI/i).first();
  // the catalog's first keyword search after a while can take several seconds
  await expect(result).toBeVisible({ timeout: 60_000 });
  // a tap selects (phones have no checkboxes)
  await result.click();
  await command(page, "Add selected");

  await expect(topBlade(page).getByText("Delivery frequencies")).toBeVisible();
  await expect(page.getByText("Has unsaved changes")).toBeVisible();
  await command(page, "Reset");
  await expect(topBlade(page).locator(".ps-chip")).toHaveCount(before);
});

test("a plan is created and deleted", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  await command(page, "Plans");
  await command(page, "Add");
  const plan = topBlade(page);
  await expect(plan.getByText("New subscription plan")).toBeVisible();
  const name = `E2E plan ${Date.now()}`;
  await plan.getByPlaceholder("e.g. Subscribe & Save").fill(name);
  // inactive, so the storefront never offers it
  // the switch's input is visually hidden behind its track: clicked directly
  const active = plan.getByRole("switch");
  await active.evaluate((input: HTMLInputElement) => input.click());
  await expect(active).not.toBeChecked();
  await command(page, "Save");
  await expect(topBlade(page).getByText(name).first()).toBeVisible();
  await expect(page).toHaveURL(/product-subscription-plan\/[0-9a-f-]{36}/);

  // the main button on a phone is Refresh, not Delete
  await command(page, "Delete");
  await expect(page.getByText(/Delete this plan\?/)).toBeVisible();
  await confirm(page);
  await expect(topBlade(page).getByText("Subscription plans")).toBeVisible();
  await expect(page.getByText(name)).toHaveCount(0);
});

test("the scheduler runs on demand", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  await command(page, "Run scheduler");
  await expect(page.getByText(/Scheduler processed \d+ renewals?\./)).toBeVisible();
});
