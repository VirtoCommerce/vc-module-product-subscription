import { flushPromises } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { toValue } from "vue";
import { getCartSubscriptionItems } from "./api/graphql";
import { useSubscriptionCheckout } from "./composables/useSubscriptionCheckout";
import { TERMS_GUARD_ID } from "./constants";
import { init } from "./index";
import type { SubscriptionCartItemFieldsFragment } from "./api/graphql/types";
import type { CheckoutGuardType } from "@vc-frontend/core";

const h = vi.hoisted(() => ({ registerGuard: vi.fn() }));

vi.mock("@vc-frontend/core", async (importOriginal) => {
  const { ref } = await import("vue");
  return {
    ...(await importOriginal<Record<string, unknown>>()),
    globals: { router: { addRoute: vi.fn() } },
    EXTENSION_NAMES: {
      productPage: {},
      cartLineItem: {},
      orderLineItem: {},
      orderListItem: {},
      orderDetails: {},
      checkoutReview: {},
    },
    useUser: () => ({ isAuthenticated: ref(true) }),
    useCheckoutGuards: () => ({ registerGuard: h.registerGuard }),
  };
});

vi.mock("./i18n", () => ({ loadLocales: vi.fn() }));

vi.mock("./api/graphql", () => ({ getCartSubscriptionItems: vi.fn(), getSubscriptionOffers: vi.fn() }));

// The terms block is loaded on demand, so the guard is registered at start-up and reads the cart the block reviews.
describe("product subscriptions plugin init", () => {
  it("registers the checkout guard, which holds a subscription cart until the terms are accepted", async () => {
    vi.mocked(getCartSubscriptionItems).mockResolvedValue([
      { lineItemId: "line-1" } as SubscriptionCartItemFieldsFragment,
    ]);
    init();
    const [id, guard] = h.registerGuard.mock.calls[0] as [string, CheckoutGuardType];

    expect(id).toBe(TERMS_GUARD_ID);
    expect(toValue(guard.canPlaceOrder)).toBe(true);

    useSubscriptionCheckout().cart.value = { id: "cart-1", items: [{ id: "line-1" }] };
    await flushPromises();

    expect(toValue(guard.canPlaceOrder)).toBe(false);
  });
});
