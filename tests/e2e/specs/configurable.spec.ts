import { expect, test, type Locator, type Page } from "@playwright/test";

// Subscribe & Save on a configurable product, on a desktop and on a phone, as a signed-in buyer: the product page
// prices the subscription from the chosen options, the cart and the checkout terms show what every delivery brings,
// and the account shows the stored configuration read-only. The cart scenario takes the product out again and never
// places an order.

test.describe.configure({ mode: "serial" });

// A configurable product an active plan covers. The local demo catalog's hat unless the environment names another.
const CONFIGURABLE_PRODUCT_PATH = process.env.E2E_CONFIGURABLE_PRODUCT_PATH ?? "/lake-shirts-en/configurable-hat";

const SUBSCRIBE_PRICE = "purchase-option-subscribe-price";
const ONCE_PRICE = "purchase-option-once-price";

// a dev server may still be compiling the page on its first visit from a new browser (a blank page until then)
async function open(page: Page, path: string) {
  await page.goto(path);
  await expect(page.locator("main").first()).toBeVisible({ timeout: 120_000 });
}

async function openConfigurableProduct(page: Page) {
  await open(page, CONFIGURABLE_PRODUCT_PATH);
  await expect(page.getByTestId("purchase-option-subscribe")).toBeVisible({ timeout: 60_000 });
}

/** "$1,234.50" → 1234.5 */
function amountOf(text: string | null): number {
  return Number((text ?? "").replace(/[^\d.,-]/g, "").replace(/,/g, ""));
}

async function priceOf(locator: Locator): Promise<number> {
  await expect(locator).toBeVisible();
  return amountOf(await locator.textContent());
}

/** Picks the n-th product of the first section that offers more than one (its radio: the card's name is a link). */
async function pickOption(page: Page, index: number) {
  const section = page.getByTestId("section").filter({ has: page.getByTestId("product-option").nth(1) }).first();
  await section.getByTestId("product-option").nth(index).locator(".vc-radio-button__indicator").click();
  await expect(section.getByTestId("product-option").nth(index).getByRole("radio")).toBeChecked();
}

/** The cart scenario needs the buyer's session: an expired one leaves the page anonymous ("Sign in to subscribe"). */
async function expectSignedIn(page: Page) {
  await expect(page.getByText("Sign in to subscribe."), "the saved buyer session has expired: run the storefront-setup project").toHaveCount(0);
}

/** Removes the configurable product's lines from the cart, so every scenario starts and ends with none. */
async function removeConfiguredLines(page: Page) {
  await open(page, "/cart");
  // the configurable product's own lines: the buyer's cart may hold other subscribed lines
  const lines = page.locator(".vc-line-item").filter({ hasText: /hat/i });
  const productLine = lines.locator('[data-test-id^="cart-line-subscription-change-"]');
  for (let i = 0; i < 5; i++) {
    const remove = lines.getByRole("button", { name: /remove/i });
    if ((await remove.count()) === 0) {
      break;
    }
    await remove.first().click();
    await page.waitForLoadState("networkidle");
  }
  await expect(productLine).toHaveCount(0);
}

/**
 * Blocks every request that would place an order or charge the buyer, and remembers it: the cart scenario only looks at
 * the checkout and must never place an order.
 */
async function guardAgainstOrders(page: Page) {
  const blocked: string[] = [];
  await page.route("**/graphql**", async (route) => {
    const body = route.request().postData() ?? "";
    const match = /(CreateOrderFromCart|OrderProductSubscriptionNow|AddOrUpdateOrderPayment|InitializePayment|AuthorizePayment)/i.exec(body);
    if (match) {
      blocked.push(match[1]);
      return route.abort();
    }
    return route.fallback();
  });
  return blocked;
}

test("a configurable product offers Subscribe & Save priced from its options", async ({ page }) => {
  test.setTimeout(180_000);
  await openConfigurableProduct(page);

  await page.getByTestId("purchase-option-subscribe").click();
  await expect(page.getByTestId("purchase-option-frequency")).toBeVisible();
  // every delivery is this configuration: said next to the choice
  await expect(page.getByTestId("purchase-option-configuration-note")).toBeVisible();

  await pickOption(page, 0);
  const once = await priceOf(page.getByTestId(ONCE_PRICE));
  const subscribed = await priceOf(page.getByTestId(SUBSCRIBE_PRICE));
  expect(subscribed, "the subscription price includes the plan discount").toBeLessThanOrEqual(once);

  // another option: both prices follow the configured price, and the discount keeps its share of it
  await pickOption(page, 1);
  await expect(async () => {
    const nextOnce = await priceOf(page.getByTestId(ONCE_PRICE));
    const nextSubscribed = await priceOf(page.getByTestId(SUBSCRIBE_PRICE));
    expect(nextSubscribed).toBeLessThanOrEqual(nextOnce);
    if (once > 0 && nextOnce !== once) {
      expect(nextSubscribed / nextOnce).toBeCloseTo(subscribed / once, 2);
    }
  }).toPass({ timeout: 15_000 });
});

test("a configured line is subscribed in the cart and the checkout shows its composition", async ({ page }) => {
  test.setTimeout(300_000);
  const blocked = await guardAgainstOrders(page);
  await removeConfiguredLines(page);

  try {
    await openConfigurableProduct(page);
    await pickOption(page, 0);
    await page.getByTestId("purchase-option-subscribe").click();
    await expectSignedIn(page);
    await page.locator(".purchase-options").getByRole("button", { name: /add to cart/i }).click();
    // the choice is applied once the server has saved the line
    await expect(page.locator(".purchase-options__status")).toContainText("In your cart as a subscription", { timeout: 60_000 });

    await open(page, "/cart");
    await expect(page.locator('[data-test-id^="cart-line-subscription-change-"]').first()).toBeVisible({ timeout: 30_000 });

    const terms = page.getByTestId("checkout-subscription-terms");
    await expect(terms).toBeVisible();
    await expect(terms.locator('[data-test-id^="checkout-subscription-composition-"]').first()).toBeVisible();
    await expect(page.getByTestId("place-order-button")).toBeDisabled();
  } finally {
    await removeConfiguredLines(page);
  }
  expect(blocked, "requests that would have placed an order").toEqual([]);
});

test("the account shows a configured subscription's configuration read-only", async ({ page }) => {
  await open(page, "/account/subscriptions");
  await expect(page.locator('[data-test-id^="subscription-delivery-"]').first()).toBeVisible({ timeout: 60_000 });

  const configuration = page.locator('[data-test-id^="subscription-configuration-"]').first();
  test.skip((await configuration.count()) === 0, "the buyer has no subscription to a configurable product");

  // the cart's and order's "Components list": folded until opened, and with no "Edit configuration"
  await expect(configuration).toBeVisible();
  await configuration.getByRole("button", { name: "Components list" }).click();
  await expect(configuration.locator(".configuration-items__row").first()).toBeVisible();
  await expect(configuration.getByRole("link", { name: /edit configuration/i })).toHaveCount(0);
  await expect(configuration.locator("input, select, textarea")).toHaveCount(0);
});

test("a delivery whose configuration changed asks to subscribe again or skip", async ({ page }) => {
  await open(page, "/account/subscriptions");
  await expect(page.locator('[data-test-id^="subscription-delivery-"]').first()).toBeVisible({ timeout: 60_000 });

  const subscribeAgain = page.getByTestId("subscription-delivery-subscribe-again-button").first();
  test.skip((await subscribeAgain.count()) === 0, "no delivery is waiting because its configuration changed");

  const delivery = page.locator('[data-test-id^="subscription-delivery-"]').filter({ has: subscribeAgain });
  await expect(delivery.getByTestId("subscription-delivery-issue")).toContainText("never send a different configuration");
  const configuration = delivery.locator('[data-test-id^="subscription-configuration-"]').first();
  await configuration.getByRole("button", { name: "Components list" }).click();
  await expect(configuration).toContainText("no longer available");
  // it leads to the product, to choose the options again
  await expect(subscribeAgain).toHaveAttribute("href", /.+/);
});
