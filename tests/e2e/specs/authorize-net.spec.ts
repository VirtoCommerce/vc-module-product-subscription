import { expect, test, type Page } from "@playwright/test";

// Saved cards with Authorize.Net against its sandbox, on a desktop and on a phone, as the signed-in buyer.
// Opt-in (E2E_AUTHORIZE_NET=1): the store needs the Authorize.Net payment method active with sandbox credentials, and the
// storefront must be served over HTTPS (Accept.js refuses plain HTTP). The card added here is removed again.

test.describe.configure({ mode: "serial" });
test.skip(process.env.E2E_AUTHORIZE_NET !== "1", "Set E2E_AUTHORIZE_NET=1 to run the Authorize.Net sandbox scenarios.");

// Authorize.Net's published sandbox Visa; its card code filter matches "900".
const TEST_CARD = { number: "4111111111111111", expiry: "12/30", cardCode: "900", last4: "1111" };

async function open(page: Page, path: string) {
  await page.goto(path);
  await expect(page.locator("main").first()).toBeVisible({ timeout: 120_000 });
}

async function removeCardIfPresent(page: Page, last4: string) {
  const remove = page.getByTestId(`saved-card-remove-${last4}`);
  if ((await remove.count()) === 0) return;
  await remove.first().click();
  await page.getByRole("dialog").getByRole("button").last().click();
  await expect(page.getByTestId(`saved-card-${last4}`)).toHaveCount(0, { timeout: 30_000 });
}

test("a card is saved with Authorize.Net, verified by the bank, and removed", async ({ page }) => {
  test.setTimeout(180_000);
  await open(page, "/account/payment-methods");
  await expect(page.getByRole("heading", { name: "Payment methods" })).toBeVisible();
  // a leftover from an interrupted run would make the new card a duplicate
  await removeCardIfPresent(page, TEST_CARD.last4);

  // with more than one payment method that saves cards, the page asks which one to use
  // (the styled radio's indicator takes the pointer, so its label text is clicked; a click that lands before the page
  // is interactive is lost, so it is repeated until the Authorize.Net form shows)
  const street = page.getByRole("textbox", { name: "Street address" });
  const providers = page.getByRole("radiogroup");
  await expect(async () => {
    if (await providers.count()) {
      await providers.getByText(/^(AuthorizeNet|Authorize\.Net)/).click();
    }
    await expect(street).toBeVisible({ timeout: 5_000 });
  }).toPass({ timeout: 60_000 });

  await page.getByRole("textbox", { name: "Card number" }).fill(TEST_CARD.number);
  await page.getByRole("textbox", { name: "Cardholder name" }).fill("Test Buyer");
  await page.getByRole("textbox", { name: "Expiration date" }).fill(TEST_CARD.expiry);
  await page.getByRole("textbox", { name: "Security code" }).fill(TEST_CARD.cardCode);

  // the $0 verification needs the card's billing street address and ZIP code; they start from the buyer's address
  const zip = page.getByRole("textbox", { name: "ZIP code" });
  if (!(await street.inputValue())) await street.fill("1 Main St");
  if (!(await zip.inputValue())) await zip.fill("98004");

  await page.getByTestId("saved-card-add").click();

  const saved = page.getByTestId(`saved-card-${TEST_CARD.last4}`);
  await expect(saved).toBeVisible({ timeout: 60_000 });
  await expect(saved).toContainText("Visa");
  await expect(saved).toContainText("12/2030");

  await removeCardIfPresent(page, TEST_CARD.last4);
});
