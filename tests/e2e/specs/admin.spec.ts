import { expect, test } from "@playwright/test";
import { command, confirm, hasCommand, isPhone, openRoute, topBlade, watchConsole } from "./shell";

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

test("plans have their own menu entry", async ({ page }, testInfo) => {
  await page.goto("/apps/admin-shell/#/");
  if (isPhone(testInfo)) await page.locator(".as-mbar__menu").click();
  await page.locator(".as-rail__item", { hasText: "More" }).click();
  await page.locator(".as-item__main", { hasText: "Subscription plans" }).first().click();
  // on a phone the blade's title moves into the app bar
  await expect(page.getByText("Subscription plans").first()).toBeVisible();
  await expect(page).toHaveURL(/product-subscription-plans/);
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
  await expect.poll(() => hasCommand(page, "Resume")).toBe(true);

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
  // a subscription that has renewals (the newest one may not have any yet)
  const renewals = await (await page.request.post("/api/product-subscription/renewals/search", { data: { take: 1 } })).json();
  const subscriptionId = renewals.results?.[0]?.items?.[0]?.subscriptionId as string;
  expect(subscriptionId, "a renewal with a subscription").toBeTruthy();
  await openRoute(page, `/product-subscriptions/product-subscription/${subscriptionId}`);
  const subscription = topBlade(page);
  await expect(subscription.getByText("Renewals")).toBeVisible();
  await subscription.locator(".ps-row").first().click();
  await expect(topBlade(page).getByText("Order and payment")).toBeVisible();
  await expect(topBlade(page).getByText(RENEWAL_NUMBER).first()).toBeVisible();
});

test("a plan's products are picked from the catalog, then the change is reset", async ({ page }) => {
  await openRoute(page, "/product-subscription-plans");
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
  await openRoute(page, "/product-subscription-plans");
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
  // back on the plans list (its own menu workspace; on a phone the title is in the app bar)
  await expect(page.getByText("Subscription plans").first()).toBeVisible();
  await expect(page.getByText(name)).toHaveCount(0);
});

test("the scheduler runs on demand", async ({ page }) => {
  await openRoute(page, "/product-subscriptions");
  await command(page, "Run scheduler");
  await expect(page.getByText(/Scheduler processed \d+ renewals?\./)).toBeVisible();
});

test("the customer emails are rendered: created, reminder with the price, payment action", async ({ page }) => {
  // the platform's notification journal; without an SMTP server the messages stay unsent, but each is rendered
  const response = await page.request.post("/api/notifications/journal", { data: { take: 200 } });
  expect(response.ok()).toBeTruthy();
  const { results } = (await response.json()) as { results: { notificationType: string; subject?: string; body?: string; to?: string }[] };
  const byType = (type: string) => results.filter((x) => x.notificationType === type);

  const reminder = byType("RenewalReminderEmailNotification")[0];
  expect(reminder, "a renewal reminder").toBeTruthy();
  expect(reminder.subject).toMatch(/next delivery/i);
  // the reminder carries the maximum price of the delivery (US auto-renewal rules)
  expect(reminder.body).toMatch(/\d+\.\d{2} [A-Z]{3}/);
  expect(reminder.body).toContain("charge more than this without asking you");
  expect(reminder.to).toContain("@");

  const rendered = (type: string, message: { subject?: string; body?: string }) => {
    expect(message.subject?.trim(), `${type} subject`).toBeTruthy();
    expect(message.body?.length ?? 0, `${type} body`).toBeGreaterThan(100);
  };
  for (const type of ["SubscriptionCreatedEmailNotification", "RenewalPaymentActionRequiredEmailNotification"]) {
    const message = byType(type)[0];
    expect(message, type).toBeTruthy();
    rendered(type, message);
  }
  // the suite never confirms a cancellation: checked when the journal has one
  const cancelled = byType("SubscriptionCancelledEmailNotification")[0];
  if (cancelled) rendered("SubscriptionCancelledEmailNotification", cancelled);
});

test("a contact's subscriptions open from the Customer module's contact blade", async ({ page }) => {
  const manifest = await (await page.request.get("/api/apps/admin-shell/manifest")).json();
  const plugins = ((manifest.plugins ?? []) as { id: string }[]).map((x) => x.id);
  test.skip(!plugins.includes("VirtoCommerce.Customer"), "the Customer module's Admin Shell plugin isn't installed");

  // the contact of a subscription's customer (the customer id is the customer's user account)
  const subscriptions = await (await page.request.post("/api/product-subscription/subscriptions/search", { data: { take: 1 } })).json();
  const userId = subscriptions.results?.[0]?.customerId as string;
  const user = await (await page.request.get(`/api/platform/security/users/id/${userId}`)).json();
  const memberId = user.memberId as string;
  expect(memberId).toBeTruthy();

  await openRoute(page, `/customers/customer-contact/${memberId}`);
  const widget = page.locator(".vc-widget", { hasText: "Subscriptions" });
  await expect(widget).toBeVisible();
  await expect(widget).toContainText(/\d/);
  await widget.click();

  const list = topBlade(page);
  await expect(list.getByText("Customer subscriptions")).toBeVisible();
  await list.getByText(SUBSCRIPTION_NUMBER).first().click();
  await expect(topBlade(page).getByText("Schedule")).toBeVisible();
});
