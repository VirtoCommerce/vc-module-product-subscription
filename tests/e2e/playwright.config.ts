import { defineConfig, devices } from "@playwright/test";

// End-to-end scenarios of the Product Subscriptions back office (Admin Shell plugin) and storefront (vc-frontend),
// on desktop and phone sizes, against running servers. URLs and test accounts come from the environment (README.md).
export const PLATFORM_URL = process.env.E2E_PLATFORM_URL ?? "http://localhost:5005";
export const STOREFRONT_URL = process.env.E2E_STOREFRONT_URL ?? "https://localhost:3000";

const browser = { channel: process.env.E2E_BROWSER_CHANNEL ?? "msedge", ignoreHTTPSErrors: true };
const desktop = { ...devices["Desktop Edge"], ...browser, viewport: { width: 1440, height: 900 } };
// a phone: narrow, touch, mobile user agent (the Admin Shell and vc-frontend switch to their phone layouts)
const phone = { ...devices["Pixel 7"], ...browser, defaultBrowserType: "chromium" as const };

export default defineConfig({
  testDir: "./specs",
  timeout: 90_000,
  expect: { timeout: 20_000 },
  // the scenarios change and restore shared data (a subscription, a plan): one at a time
  workers: 1,
  fullyParallel: false,
  retries: 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: { trace: "retain-on-failure", screenshot: "only-on-failure", actionTimeout: 20_000, testIdAttribute: "data-test-id" },
  projects: [
    { name: "admin-setup", testMatch: /admin\.setup\.ts/, use: { ...desktop, baseURL: PLATFORM_URL } },
    { name: "storefront-setup", testMatch: /storefront\.setup\.ts/, use: { ...desktop, baseURL: STOREFRONT_URL } },
    {
      name: "admin-desktop",
      testMatch: /admin\.spec\.ts/,
      dependencies: ["admin-setup"],
      use: { ...desktop, baseURL: PLATFORM_URL, storageState: ".auth/admin.json" },
    },
    {
      name: "admin-mobile",
      testMatch: /admin\.spec\.ts/,
      dependencies: ["admin-setup"],
      use: { ...phone, baseURL: PLATFORM_URL, storageState: ".auth/admin.json" },
    },
    {
      name: "storefront-desktop",
      testMatch: /(storefront|authorize-net)\.spec\.ts/,
      dependencies: ["storefront-setup"],
      use: { ...desktop, baseURL: STOREFRONT_URL, storageState: ".auth/buyer.json" },
    },
    {
      name: "storefront-mobile",
      testMatch: /(storefront|authorize-net)\.spec\.ts/,
      dependencies: ["storefront-setup"],
      use: { ...phone, baseURL: STOREFRONT_URL, storageState: ".auth/buyer.json" },
    },
  ],
});
