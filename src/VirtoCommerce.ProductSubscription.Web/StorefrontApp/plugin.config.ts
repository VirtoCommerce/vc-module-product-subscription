import { definePluginManifest, not, settingEnabled } from "@vc-frontend/core/manifest";
import {
  ENABLED_KEY,
  HOW_IT_WORKS_ROUTE_NAME,
  HOW_IT_WORKS_ROUTE_PATH,
  LEGACY_SUBSCRIPTION_ROUTE_NAME,
  LEGACY_SUBSCRIPTION_ROUTE_PATH,
  MODULE_ID,
  SUBSCRIPTIONS_ROUTE_NAME,
  SUBSCRIPTIONS_ROUTE_PATH,
} from "./src/constants";

// What the storefront knows before running this plugin. Reference:
// https://github.com/VirtoCommerce/vc-frontend/blob/dev/client-app/core/federation/declarations.md
//
// The header link is not declared: its title is a key from this plugin's locales and would show raw until they load.
// Checkout terms are `block`: without the plugin, a subscription order must not be placed.
export default definePluginManifest({
  when: settingEnabled(MODULE_ID, ENABLED_KEY),
  routes: [
    { path: SUBSCRIPTIONS_ROUTE_PATH, parent: "Account", name: SUBSCRIPTIONS_ROUTE_NAME },
    { path: LEGACY_SUBSCRIPTION_ROUTE_PATH, parent: "Account", name: LEGACY_SUBSCRIPTION_ROUTE_NAME },
    { path: HOW_IT_WORKS_ROUTE_PATH, name: HOW_IT_WORKS_ROUTE_NAME },
  ],
  slots: [
    { at: "productPage/purchase-options", policy: "reserve", when: (field) => not(field("hasVariations")) },
    { at: "productPage/variation-purchase-options", policy: "reserve", when: (field) => not(field("isConfigurable")) },
    { at: "cartLineItem/details", policy: "reserve" },
    { at: "cartLineItem/actions", policy: "reserve" },
    { at: "orderLineItem/details", policy: "reserve" },
    { at: "orderListItem/badge", policy: "reserve" },
    { at: "orderDetails/notice", policy: "reserve" },
    { at: "checkoutReview/before-place-order", policy: "block" },
  ],
});
