import { expect, test, type Page, type TestInfo } from "@playwright/test";

// Subscribe & Save in vc-frontend, on a desktop and on a phone, as a signed-in buyer with active subscriptions.
// The pause scenario resumes the subscription it paused.

test.describe.configure({ mode: "serial" });

const isPhone = (testInfo: TestInfo) => testInfo.project.name.endsWith("-mobile");

// A product an active subscription plan covers: the demo data's Pepsi crate unless the environment names another.
const SUBSCRIPTION_PRODUCT_SEARCH = process.env.E2E_SUBSCRIPTION_PRODUCT_SEARCH ?? "pepsi";
const SUBSCRIPTION_PRODUCT_NAME = process.env.E2E_SUBSCRIPTION_PRODUCT_NAME ?? "PEPSI COLA REGULAR CRATE";

async function openProduct(page: Page) {
  await open(page, `/search?q=${encodeURIComponent(SUBSCRIPTION_PRODUCT_SEARCH)}`);
  // the product's name, not its image (whose overlay takes the click)
  await page.getByRole("link").filter({ hasText: SUBSCRIPTION_PRODUCT_NAME }).first().click();
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

// The account page lists upcoming deliveries by month. Each delivery card has a settings menu (⚙); what it offers is
// what the store lets customers do (Product Subscription > Customer actions). The scenarios assume the defaults:
// skip, order now and change card off; pause and change frequency on. Cancelling is always offered.
const DELIVERY = '[data-test-id^="subscription-delivery-"]';
const MENU_BUTTON = '[data-test-id^="subscription-settings-"][data-test-id$="-button"]';
const FREQUENCY_CHANGE = '[data-test-id^="subscription-frequency-change-"]';

async function openDeliveries(page: Page) {
  await open(page, "/account/subscriptions");
  await expect(page.locator(DELIVERY).first()).toBeVisible({ timeout: 60_000 });
}

/** Opens the settings menu of the first delivery that has one and returns its test id prefix. */
async function openFirstMenu(page: Page) {
  const button = page.locator(MENU_BUTTON).first();
  const prefix = (await button.getAttribute("data-test-id"))!.replace(/-button$/, "");
  await button.click();
  return prefix;
}

test("the account lists upcoming deliveries by month", async ({ page }) => {
  await openDeliveries(page);
  await expect(page.getByText(/active · next delivery/)).toBeVisible();
  // skipping moved from a button on the card into the settings menu
  await expect(page.getByTestId("subscription-delivery-skip-button")).toHaveCount(0);
  await expect(page.locator(MENU_BUTTON).first()).toBeVisible();
});

test("the delivery menu offers only what the store allows", async ({ page }) => {
  await openDeliveries(page);
  const menu = await openFirstMenu(page);
  await expect(page.getByTestId(`${menu}-cancel`)).toBeVisible();
  for (const off of ["skip", "order-now", "change-card"]) {
    await expect(page.getByTestId(`${menu}-${off}`), `${off} is off by default`).toHaveCount(0);
  }
  // Escape closes the menu; its panel stays in the page, hidden
  await page.keyboard.press("Escape");
  await expect(page.getByTestId(`${menu}-cancel`)).toBeHidden();
});

test("a subscription is paused and resumed", async ({ page }) => {
  await openDeliveries(page);
  // the frequency dialog of one product pauses that product only; taken from a delivery not placed yet, so the
  // subscription is a healthy one (a past-due one would come back with its failed delivery still to pay)
  const change = page.locator(`[data-test-id^="subscription-delivery-upcoming-"] ${FREQUENCY_CHANGE}`).first();
  const subscriptionId = (await change.getAttribute("data-test-id"))!.replace("subscription-frequency-change-", "");
  await change.click();
  await expect(page.getByTestId("purchase-options-modal")).toBeVisible();
  await page.getByTestId("purchase-options-modal-pause").click();
  await page.getByTestId("subscription-confirm-modal-ok").click();
  await expect(page.getByTestId("subscription-confirm-modal")).toHaveCount(0);

  // it moves to "Paused and cancelled", folded until opened
  const inactive = page.getByTestId("subscriptions-inactive");
  const resume = page.getByTestId(`subscription-resume-${subscriptionId}`);
  await expect(inactive).toBeVisible();
  if (!(await resume.isVisible())) {
    await inactive.locator(".vc-widget__header-container").click();
  }
  await resume.click();
  // and is back among the deliveries, also after a reload
  const back = page.getByTestId(`subscription-frequency-change-${subscriptionId}`).first();
  await expect(back).toBeVisible();
  await page.reload();
  await expect(back).toBeVisible({ timeout: 60_000 });
});

test("the frequency dialog shows the plan's frequencies", async ({ page }) => {
  await openDeliveries(page);
  await page.locator(FREQUENCY_CHANGE).first().click();
  const dialog = page.getByTestId("purchase-options-modal");
  await expect(dialog).toBeVisible();
  await expect(page.getByTestId("purchase-options-modal-frequency")).toBeVisible();
  await expect(page.getByTestId("purchase-options-modal-save")).toBeVisible();
  // closed without saving
  await dialog.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(dialog).toHaveCount(0);
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
    // a subscribed line shows its subscription (frequency and a Change link) instead of the Subscribe button
    await expect(page.locator('[data-test-id^="cart-line-subscription-change-"]').first()).toBeVisible();
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
  await openDeliveries(page);
  const menu = await openFirstMenu(page);
  await page.getByTestId(`${menu}-cancel`).click();
  // one click to confirm; the scenario closes the dialog instead, keeping the subscription
  const dialog = page.getByTestId("subscription-confirm-modal");
  await expect(dialog).toBeVisible();
  await expect(page.getByTestId("subscription-confirm-modal-ok")).toBeVisible();
  await dialog.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(page.locator(MENU_BUTTON).first()).toBeVisible();
});

test("a renewal waiting for 3-D Secure is confirmed and authorized", async ({ page }, testInfo) => {
  // completes the payment for good: once, on the phone (the more demanding layout)
  test.skip(!isPhone(testInfo), "runs once, on the phone");
  await openDeliveries(page);
  const confirmButton = page.getByTestId("subscription-delivery-confirm-payment").first();
  test.skip(!(await confirmButton.isVisible()), "no delivery is waiting for the customer");
  const delivery = (await page.locator(DELIVERY, { has: confirmButton }).first().getAttribute("data-test-id"))!;

  await confirmButton.click();
  await expect(page).toHaveURL(/\/account\/orders\/.+\/payment/);
  await expect(page.getByTestId("mock-card-payment")).toBeVisible();
  await page.getByTestId("mock-card-pay").click();
  // the mock bank asks for 3-D Secure, as the real one did for this card
  await page.getByTestId("mock-3ds-approve").click();

  await expect(async () => {
    await openDeliveries(page);
    await expect(page.getByTestId(delivery).getByTestId("subscription-delivery-confirm-payment")).toHaveCount(0);
  }).toPass({ timeout: 60_000 });
});

// Variations: each row of a B2B variations list or table, and the variation a B2C page has selected, offer Subscribe &
// Save when a plan covers the variation. The defaults are the local demo data (a plan "Demo: variations" on both).
const B2B_VARIATIONS_PATH =
  process.env.E2E_B2B_VARIATIONS_PATH ?? "/kitchen-supplies/everything-for-kitchen/BENDING-STRAWS-PAPER-STRIPE";
const B2C_VARIATIONS_PATH =
  process.env.E2E_B2C_VARIATIONS_PATH ?? "/speeds-medical/medicines-treatments/pain-relief/paracetamol-500mg";
// the B2C option that selects a variation with a price and a plan, and one that selects a variation without a price
const B2C_OPTION_WITH_PLAN = process.env.E2E_B2C_OPTION_WITH_PLAN ?? "3x8 (24)";
const B2C_OPTION_WITHOUT_PRICE = process.env.E2E_B2C_OPTION_WITHOUT_PRICE ?? "2x8 (16)";
const ROW_SUBSCRIBE = '[data-test-id^="purchase-option-subscribe-"]';
const ROW_FREQUENCY = '[data-test-id^="purchase-option-frequency-"]';
const CART_LINE_SUBSCRIPTION = '[data-test-id^="cart-line-subscription-change-"]';

async function openB2bVariations(page: Page) {
  await open(page, B2B_VARIATIONS_PATH);
  await expect(page.locator(ROW_SUBSCRIBE).first()).toBeAttached({ timeout: 60_000 });
}

/**
 * Takes the first variation row out of the cart: a line already in the cart brings its subscription with it (the row
 * shows the box ticked), and the scenarios start from a row that isn't bought yet.
 */
async function clearFirstVariationRow(page: Page) {
  await openB2bVariations(page);
  const stepper = page.locator(".purchase-options--compact").first().getByTestId("quantity-stepper");
  for (let i = 0; i < 6; i++) {
    const value = await stepper.locator("input").inputValue();
    if (EMPTY.test(value)) return;
    await stepper.getByRole("button", { name: "Decrease quantity" }).click();
    await expect(stepper.locator("input")).not.toHaveValue(value);
    await page.waitForTimeout(3_000);
  }
  throw new Error("the variation row didn't leave the cart");
}

/** Ticks a row's Subscribe & Save (once: a second click would untick it) and expects its frequency. */
async function subscribeRow(row: ReturnType<Page["locator"]>) {
  const box = row.locator(ROW_SUBSCRIBE);
  if (!(await box.isChecked())) await row.getByText("Subscribe & Save").click();
  await expect(box).toBeChecked();
  await expect(row.locator(ROW_FREQUENCY)).toBeVisible();
}

test("each variation row offers Subscribe & Save, in the list and in the table", async ({ page }, testInfo) => {
  test.setTimeout(180_000);
  await clearFirstVariationRow(page);
  const row = page.locator(".purchase-options--compact").first();
  // one checkbox under the row's own add-to-cart; the frequency only once it is ticked
  await expect(row.getByTestId("quantity-stepper")).toBeVisible();
  await expect(row.locator(ROW_SUBSCRIBE)).not.toBeChecked();
  await expect(row.locator(ROW_FREQUENCY)).toHaveCount(0);
  await subscribeRow(row);

  // a phone has no table view
  if (isPhone(testInfo)) return;
  await page.getByText("Table", { exact: true }).click();
  const tableRow = page.locator("table .purchase-options--compact, .variations-table .purchase-options--compact").first();
  await expect(tableRow.locator(ROW_SUBSCRIBE)).toBeAttached();
  await subscribeRow(tableRow);
});

test("a variation added with Subscribe & Save is subscribed in the cart", async ({ page }) => {
  test.setTimeout(240_000);
  await clearFirstVariationRow(page);
  await open(page, "/cart");
  const before = await page.locator(CART_LINE_SUBSCRIPTION).count();

  await openB2bVariations(page);
  const row = page.locator(".purchase-options--compact").first();
  const stepper = row.getByTestId("quantity-stepper");
  try {
    await subscribeRow(row);
    const value = await stepper.locator("input").inputValue();
    await stepper.getByRole("button", { name: "Increase quantity" }).click();
    await expect(stepper.locator("input")).not.toHaveValue(value);
    // the choice is applied once the server has saved the line: the row says so (leaving earlier would drop it)
    await expect(row.locator(".purchase-options__status")).toContainText("In your cart as a subscription", { timeout: 30_000 });

    // the cart shows the line subscribed
    await expect(async () => {
      await open(page, "/cart");
      await expect(page.locator(CART_LINE_SUBSCRIPTION)).toHaveCount(before + 1, { timeout: 5_000 });
    }).toPass({ timeout: 60_000 });
  } finally {
    // the scenario leaves the variation out of the cart
    await clearFirstVariationRow(page);
  }
});

test("the selected B2C variation offers the same purchase options as a simple product", async ({ page }) => {
  test.setTimeout(180_000);
  await open(page, B2C_VARIATIONS_PATH);
  // until a variation is selected there is nothing to buy
  await expect(page.getByTestId("purchase-option-subscribe")).toHaveCount(0);

  await page.getByRole("button", { name: B2C_OPTION_WITH_PLAN, exact: true }).click();
  await expect(page.getByTestId("purchase-option-once")).toBeVisible();
  await page.getByTestId("purchase-option-subscribe").click();
  await expect(page.getByTestId("purchase-option-frequency")).toBeVisible();

  // a variation without a price can't be bought: no purchase options
  await page.getByRole("button", { name: B2C_OPTION_WITHOUT_PRICE, exact: true }).click();
  await expect(page.getByTestId("purchase-option-subscribe")).toHaveCount(0);

  // back to the first one: it starts again from one-time (nothing of it is in the cart)
  await page.getByRole("button", { name: B2C_OPTION_WITH_PLAN, exact: true }).click();
  await expect(page.getByTestId("purchase-option-frequency")).toHaveCount(0);
});
