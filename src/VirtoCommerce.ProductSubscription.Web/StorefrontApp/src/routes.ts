import {
  HOW_IT_WORKS_ROUTE_NAME,
  HOW_IT_WORKS_ROUTE_PATH,
  LEGACY_SUBSCRIPTION_ROUTE_NAME,
  LEGACY_SUBSCRIPTION_ROUTE_PATH,
  SUBSCRIPTIONS_ROUTE_NAME,
  SUBSCRIPTIONS_ROUTE_PATH,
} from "./constants";
import type { RouteRecordRaw } from "vue-router";

const Subscriptions = () => import("./pages/subscriptions.vue");
const HowItWorks = () => import("./pages/how-it-works.vue");

/** Account routes, added under the host's Account route. */
export const routes: RouteRecordRaw[] = [
  { path: SUBSCRIPTIONS_ROUTE_PATH, name: SUBSCRIPTIONS_ROUTE_NAME, component: Subscriptions },
  {
    path: LEGACY_SUBSCRIPTION_ROUTE_PATH,
    name: LEGACY_SUBSCRIPTION_ROUTE_NAME,
    redirect: (to) => ({ name: SUBSCRIPTIONS_ROUTE_NAME, query: { subscription: String(to.params.subscriptionId) } }),
  },
];

/** Top-level routes, open to guests: the Subscribe & Save rules linked from the product page. */
export const publicRoutes: RouteRecordRaw[] = [
  { path: HOW_IT_WORKS_ROUTE_PATH, name: HOW_IT_WORKS_ROUTE_NAME, component: HowItWorks, meta: { public: true } },
];
