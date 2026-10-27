import { expect, test, type Page, type TestInfo } from "@playwright/test";

// Subscribe & Save in vc-frontend, on a desktop and on a phone, as a signed-in buyer with active subscriptions.
// The pause scenario resumes the subscription it paused.

test.describe.configure({ mode: "serial" });

const isPhone = (testInfo: TestInfo) => testInfo.project.name.endsWith("-mobile");

async function openProduct(page: Page) {
  await open(page, "/search?q=pepsi");
  // the product's name, not its image (whose overlay takes the click)
  await page.getByRole("link").filter({ hasText: /PEPSI COLA REGULAR CRATE/i }).first().click();
  await expect(page.getByTestId("purchase-option-subscribe")).toBeVisible();
}

// a dev server may still be compiling the page on its first visit from a new browser (a blank page until then)
async function open(page: Page, path: string) {
  await page.goto(path);
  await expect(page.locator("main").first()).toBeVisible({ timeout: 120_000 });
}

test("the product page offers Subscribe & Save with a frequency", async ({ page }) => {
  test.setTimeout(180_000);
  await openProduct(page);
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

  // the status tabs switch the list
  const paused = page.getByTestId("subscriptions-tab-paused").getByRole("button");
  await paused.click();
  await expect(paused).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByTestId("subscriptions-tab-active").getByRole("button")).toHaveAttribute("aria-pressed", "false");
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
  // the status badge follows the server: resume only once the pause is saved, or the two requests race
  await expect(page.getByTestId("status-Paused")).toBeVisible();
  await page.getByTestId("subscription-resume").click();
  await expect(page.getByTestId("status-Active")).toBeVisible();
  await expect(page.getByTestId("subscription-pause")).toBeVisible();
  // and the server agrees after a reload
  await page.reload();
  await expect(page.getByTestId("status-Active")).toBeVisible({ timeout: 60_000 });
});

test("saved payment methods are listed", async ({ page }) => {
  await open(page, "/account/payment-methods");
  await expect(page.locator('[data-test-id^="saved-card-"]').first()).toBeVisible();
  await expect(page.getByTestId("saved-card-add")).toBeVisible();
});

const EMPTY = /^0?$/;

/**
 * Sets the product's quantity in the cart with the product page's stepper, one pack per click. The stepper sends its
 * change after a short delay, so each step is checked on a reloaded page.
 */
async function setPacks(page: Page, wanted: "one" | "none") {
  for (let i = 0; i < 6; i++) {
    await openProduct(page);
    await page.getByTestId("purchase-option-subscribe").click();
    const stepper = page.getByTestId("quantity-stepper");
    const value = await stepper.locator("input").inputValue();
    const empty = EMPTY.test(value);
    if (wanted === "one" ? !empty : empty) return;
    await stepper.getByRole("button", { name: wanted === "one" ? "Increase quantity" : "Decrease quantity" }).click();
    await expect(stepper.locator("input")).not.toHaveValue(value);
    await page.waitForTimeout(3_000);
  }
  throw new Error(`the product's quantity didn't reach ${wanted}`);
}

/**
 * Blocks every request that would place an order or charge the buyer, and remembers it: a scenario that only looks at
 * the checkout must never place an order, whatever it clicks.
 */
async function guardAgainstOrders(page: Page) {
  const blocked: string[] = [];
  await page.route("**/graphql**", async (route) => {
    const body = route.request().postData() ?? "";
    const match = /(CreateOrderFromCart|OrderProductSubscriptionNow|AddOrUpdateOrderPayment|InitializePayment|AuthorizePayment)/i.exec(body);
    if (match) {
      blocked.push(match[1]);
      return route.abort();
    }
    return route.fallback();
  });
  return blocked;
}

test("checkout asks for the subscription terms before the order can be placed", async ({ page }) => {
  test.setTimeout(240_000);
  const blocked = await guardAgainstOrders(page);
  // adds the product as a subscription and always takes it out again
  await setPacks(page, "one");
  try {
    await open(page, "/cart");
    await expect(page.locator('[data-test-id^="cart-line-subscribe-"]').first()).toBeVisible();
    const terms = page.getByTestId("checkout-subscription-terms");
    await expect(terms).toBeVisible();
    // US auto-renewal rules: the consent box starts unchecked and the order can't be placed without it
    const consent = page.getByTestId("checkout-subscription-terms-checkbox");
    // toggled with the keyboard: no pointer click near the order buttons
    const toggle = async () => {
      await consent.focus();
      await page.keyboard.press("Space");
    };
    await expect(consent).not.toBeChecked();
    await expect(terms.getByText("Please accept the terms to place your order.")).toBeVisible();
    await expect(page.getByTestId("place-order-button")).toBeDisabled();

    // accepting records a consent for the cart (its id is kept for the payment step)
    await toggle();
    await expect(consent).toBeChecked();
    await expect(terms.getByText("Please accept the terms to place your order.")).toHaveCount(0);
    const stored = await page.evaluate(() => Object.values(sessionStorage).find((v) => v.includes("consentId")));
    expect(JSON.parse(stored ?? "{}").consentId).toBeTruthy();
    await toggle();
    await expect(consent).not.toBeChecked();
  } finally {
    await setPacks(page, "none");
  }
  expect(blocked, "requests that would have placed an order").toEqual([]);
});

test("cancelling a subscription takes one confirmation", async ({ page }) => {
  await open(page, "/account/subscriptions");
  await page.locator('[data-test-id^="subscription-row-"]', { hasText: "Active" }).first().click();
  await page.getByTestId("subscription-cancel").click();
  // one click to confirm; the scenario closes the dialog instead, keeping the subscription
  await expect(page.getByTestId("subscription-cancel-confirm")).toBeVisible();
  await page.getByRole("button", { name: "Keep subscription" }).click();
  await expect(page.getByTestId("subscription-cancel-confirm")).toHaveCount(0);
  await expect(page.getByTestId("subscription-pause")).toBeVisible();
});

test("a renewal waiting for 3-D Secure is confirmed and authorized", async ({ page }, testInfo) => {
  // completes the payment for good: once, on the phone (the more demanding layout)
  test.skip(!isPhone(testInfo), "runs once, on the phone");
  await open(page, "/account/subscriptions");
  const confirmButton = page.getByTestId("renewal-confirm-payment").first();
  await page.locator('[data-test-id^="subscription-row-"]').first().waitFor();
  test.skip(!(await confirmButton.isVisible()), "no renewal is waiting for the customer");
  const renewal = (await page.locator('[data-test-id^="renewal-"]', { has: confirmButton }).first().getAttribute("data-test-id"))!;

  await confirmButton.click();
  await expect(page).toHaveURL(/\/account\/orders\/.+\/payment/);
  await expect(page.getByTestId("mock-card-payment")).toBeVisible();
  await page.getByTestId("mock-card-pay").click();
  // the mock bank asks for 3-D Secure, as the real one did for this card
  await page.getByTestId("mock-3ds-approve").click();

  await expect(async () => {
    await open(page, "/account/subscriptions");
    await expect(page.getByTestId(renewal).getByTestId("renewal-confirm-payment")).toHaveCount(0);
  }).toPass({ timeout: 60_000 });
});
