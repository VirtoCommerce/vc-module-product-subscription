import * as blades from "./pages";
import * as locales from "./locales";
import { defineAppModule, registerExternalWidget, usePermissions } from "@vc-shell/framework";
import { markRaw } from "vue";
import CustomerSubscriptionsWidget from "./widgets/CustomerSubscriptionsWidget.vue";
import "./styles.scss";

// The contact's subscriptions, on the Customer module's contact blade (when its Admin Shell plugin is installed)
registerExternalWidget({
  id: "ProductSubscriptionCustomerSubscriptions",
  title: "Subscriptions",
  component: markRaw(CustomerSubscriptionsWidget),
  targetBlades: ["CustomerContactDetails"],
  isVisible: () => usePermissions().hasAccess("product-subscription:read"),
});

export default defineAppModule({
  blades,
  locales,
});
