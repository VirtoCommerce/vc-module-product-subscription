import { expect, type Page, type TestInfo } from "@playwright/test";

export const isPhone = (testInfo: TestInfo) => testInfo.project.name.endsWith("-mobile");

/** The blade on top: the only one on a phone, the rightmost on a desktop */
export const topBlade = (page: Page) => page.locator(".vc-blade").last();

export async function openRoute(page: Page, route: string) {
  await page.goto(`/apps/admin-shell/#${route}`);
  await expect(topBlade(page)).toBeVisible();
}

/**
 * Runs a toolbar command of the top blade by its title. Desktop: a toolbar button, or the toolbar's "More" menu when
 * the blade is narrow. Phone: the floating main button, or its menu.
 */
export async function command(page: Page, title: string) {
  const blade = topBlade(page);
  const pill = blade.locator(".vc-blade-toolbar-mobile__pill-action");
  await blade.locator(".vc-blade-toolbar, .vc-blade-toolbar-mobile").first().waitFor({ state: "visible" });
  if (await pill.isVisible()) {
    if ((await pill.getAttribute("aria-label")) === title) return pill.click();
    await blade.locator(".vc-blade-toolbar-mobile__pill-more").click();
    return page.locator(`.vc-blade-toolbar-mobile__action[aria-label="${title}"]`).last().click();
  }
  const button = blade.locator(".vc-blade-toolbar").getByRole("button", { name: title, exact: true }).first();
  // the toolbar redraws while the blade reloads after a command: give the button a moment to come back
  await button.waitFor({ state: "visible", timeout: 5_000 }).catch(() => undefined);
  if (await button.isVisible()) {
    await expect(button).toBeEnabled();
    return button.click();
  }
  await blade.locator(".vc-blade-toolbar").getByRole("button", { name: "More", exact: true }).click();
  await page.getByRole("menuitem", { name: title, exact: true }).last().click();
}

/** Whether a command is offered (enabled) in the top blade's toolbar */
export async function hasCommand(page: Page, title: string) {
  const blade = topBlade(page);
  const pill = blade.locator(".vc-blade-toolbar-mobile__pill-action");
  if (await pill.isVisible()) {
    if ((await pill.getAttribute("aria-label")) === title) return true;
    return (await page.locator(`.vc-blade-toolbar-mobile__action[aria-label="${title}"]`).count()) > 0;
  }
  const items = blade.locator(".vc-blade-toolbar").getByRole("button", { name: title, exact: true });
  if ((await items.count()) && (await items.first().isEnabled())) return true;
  return (await page.getByRole("menuitem", { name: title, exact: true }).count()) > 0;
}

/** Answers the framework's confirmation popup */
export async function confirm(page: Page) {
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
}

/** Collects console errors of the page, ignoring the platform's push-notification reconnects */
export function watchConsole(page: Page) {
  const errors: string[] = [];
  page.on("console", (message) => {
    if (message.type() !== "error") return;
    const text = message.text();
    if (/negotiation|WebSocket|ERR_CONNECTION_REFUSED|signalr|favicon/i.test(text)) return;
    errors.push(text);
  });
  return errors;
}
