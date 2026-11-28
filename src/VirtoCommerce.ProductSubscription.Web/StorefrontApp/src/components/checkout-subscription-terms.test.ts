import { flushPromises, mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useSubscriptionCheckout } from "../composables/useSubscriptionCheckout";
import CheckoutSubscriptionTerms from "./checkout-subscription-terms.vue";
import type { CartType } from "@vc-frontend/core";
import type { MaybeRefOrGetter } from "vue";

const h = vi.hoisted(() => ({
  acceptSubscriptionTerms: vi.fn(),
  getSubscriptionTerms: vi.fn(),
  refresh: vi.fn(),
}));

const state = vi.hoisted(() => ({
  /** What the card-saving capability holds: the consent a card form saves the card with. */
  cardSaving: undefined as { consentId?: string; cartId: string } | undefined,
  placingOrder: undefined as unknown as { value: boolean },
  subscribedItems: undefined as unknown as { value: { lineItemId: string }[] },
  loading: undefined as unknown as { value: boolean },
  failed: undefined as unknown as { value: boolean },
}));

vi.mock("vue-i18n", () => ({
  useI18n: () => ({ t: (key: string) => key }),
}));

vi.mock("@vc-frontend/core", async (importOriginal) => {
  const { ref } = await import("vue");
  state.placingOrder = ref(false);
  return {
    ...(await importOriginal<Record<string, unknown>>()),
    globals: { storeId: "store-1" },
    Logger: { error: vi.fn(), warn: vi.fn() },
    prepareLineItem: (item: { id: string; name: string }) => ({ id: item.id, name: item.name }),
    useCurrentCart: () => ({ availablePaymentMethods: ref([{ code: "card" }]) }),
    useOrderPlacement: () => ({ placingOrder: state.placingOrder }),
    useCardSavingRequirement: () => ({
      require: (consentId: string | undefined, cartId: string) => {
        state.cardSaving = { consentId, cartId };
      },
      clear: () => {
        state.cardSaving = undefined;
      },
    }),
  };
});

vi.mock("../api/graphql", () => ({
  acceptSubscriptionTerms: h.acceptSubscriptionTerms,
  getSubscriptionTerms: h.getSubscriptionTerms,
}));

vi.mock("../composables/useCartSubscriptions", async () => {
  const { computed, ref, toValue } = await import("vue");
  state.subscribedItems = ref([]);
  state.loading = ref(false);
  state.failed = ref(false);
  return {
    useCartSubscriptions: (cartSource: MaybeRefOrGetter<{ id?: string } | undefined>) => ({
      subscribedItems: state.subscribedItems,
      hasSubscriptionItems: computed(() => !!toValue(cartSource)?.id && state.subscribedItems.value.length > 0),
      loading: state.loading,
      failed: state.failed,
      refresh: h.refresh,
    }),
  };
});

const cart = { id: "cart-1", items: [{ id: "line-1", name: "Coffee" }] } as unknown as CartType;
const cartWithoutSubscribedLine = { id: "cart-1", items: [{ id: "line-2", name: "Mug" }] } as unknown as CartType;

function mountTerms() {
  return mount(CheckoutSubscriptionTerms, {
    props: { cart },
    global: {
      stubs: {
        VcWidget: { template: "<div><slot /></div>" },
        VcAlert: { template: "<div><slot /></div>" },
        VcButton: true,
        VcLoader: true,
        VcLineItems: true,
        VcMarkdownRender: true,
        VcCheckbox: { name: "VcCheckbox", props: ["modelValue"], template: "<input type='checkbox' />" },
      },
      mocks: { $t: (key: string) => key },
    },
  });
}

async function tickTerms(wrapper: ReturnType<typeof mountTerms>) {
  wrapper.findComponent({ name: "VcCheckbox" }).vm.$emit("update:model-value", true);
  await flushPromises();
}

function storedRequirement() {
  return state.cardSaving;
}

describe("CheckoutSubscriptionTerms: stored card-saving consent", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    state.cardSaving = undefined;
    state.placingOrder.value = false;
    state.subscribedItems.value = [{ lineItemId: "line-1" }];
    h.getSubscriptionTerms.mockResolvedValue({ text: "Terms", version: "1", hash: "hash" });
    h.acceptSubscriptionTerms.mockResolvedValue("consent-1");
  });

  it("stores the consent when the terms are ticked", async () => {
    const wrapper = mountTerms();
    await flushPromises();

    await tickTerms(wrapper);

    expect(storedRequirement()).toMatchObject({ consentId: "consent-1", cartId: "cart-1" });
  });

  it("clears a consent stored earlier when the block mounts with the box unticked", async () => {
    state.cardSaving = { consentId: "old", cartId: "cart-1" };

    mountTerms();
    await flushPromises();

    expect(storedRequirement()).toBeUndefined();
  });

  it("clears the consent when the subscribed line leaves the cart and other lines stay", async () => {
    const wrapper = mountTerms();
    await flushPromises();
    await tickTerms(wrapper);

    await wrapper.setProps({ cart: cartWithoutSubscribedLine });
    state.subscribedItems.value = [];
    await flushPromises();

    expect(storedRequirement()).toBeUndefined();
  });

  it("clears the consent when the subscribed line is turned one-time", async () => {
    const wrapper = mountTerms();
    await flushPromises();
    await tickTerms(wrapper);

    state.subscribedItems.value = [];
    await flushPromises();

    expect(storedRequirement()).toBeUndefined();
  });

  it("clears the consent when the block is dropped, e.g. the cart was left empty", async () => {
    const wrapper = mountTerms();
    await flushPromises();
    await tickTerms(wrapper);

    wrapper.unmount();

    expect(storedRequirement()).toBeUndefined();
  });

  it("asks again after going back to the cart and turning the subscription off (multi-step)", async () => {
    const review = mountTerms();
    await flushPromises();
    await tickTerms(review);

    // Back to the cart: the review page and its block go away, and the subscription is turned off there.
    review.unmount();
    state.subscribedItems.value = [];

    // Back to the review: nothing to consent to, and nothing stored.
    const again = mountTerms();
    await flushPromises();

    expect(storedRequirement()).toBeUndefined();
    expect(again.findComponent({ name: "VcCheckbox" }).exists()).toBe(false);
  });

  it("keeps the consent while an order is placed, so the order's payment step still reads it", async () => {
    const wrapper = mountTerms();
    await flushPromises();
    await tickTerms(wrapper);

    // Placing the order refetches the cart (no subscribed lines left) and then leaves the review page.
    state.placingOrder.value = true;
    state.subscribedItems.value = [];
    await flushPromises();
    wrapper.unmount();

    expect(storedRequirement()).toMatchObject({ consentId: "consent-1", cartId: "cart-1" });
  });

  it("clears the consent when the box is unticked", async () => {
    const wrapper = mountTerms();
    await flushPromises();
    await tickTerms(wrapper);

    wrapper.findComponent({ name: "VcCheckbox" }).vm.$emit("update:model-value", false);
    await flushPromises();

    expect(storedRequirement()).toBeUndefined();
  });
});

// The module registers the checkout guard at start-up; the block only tells it which cart it reviews and whether the
// terms are accepted.
describe("CheckoutSubscriptionTerms: checkout guard state", () => {
  const checkout = useSubscriptionCheckout();

  beforeEach(() => {
    vi.clearAllMocks();
    state.cardSaving = undefined;
    state.placingOrder.value = false;
    state.loading.value = false;
    state.failed.value = false;
    checkout.cart.value = undefined;
    checkout.accepted.value = false;
    state.subscribedItems.value = [{ lineItemId: "line-1" }];
    h.getSubscriptionTerms.mockResolvedValue({ text: "Terms", version: "1", hash: "hash" });
    h.acceptSubscriptionTerms.mockResolvedValue("consent-1");
  });

  it("holds a subscription cart until the terms are accepted, and nothing when no cart is reviewed", async () => {
    expect(checkout.canPlaceOrder.value).toBe(true);

    const wrapper = mountTerms();
    await flushPromises();
    expect(checkout.cart.value?.id).toBe("cart-1");
    expect(checkout.canPlaceOrder.value).toBe(false);

    await tickTerms(wrapper);
    expect(checkout.canPlaceOrder.value).toBe(true);

    wrapper.unmount();
    expect(checkout.cart.value).toBeUndefined();
    expect(checkout.accepted.value).toBe(false);
    expect(checkout.canPlaceOrder.value).toBe(true);
  });

  it("holds the order while the cart's subscriptions load", async () => {
    state.loading.value = true;
    state.subscribedItems.value = [];

    const wrapper = mountTerms();
    await flushPromises();

    expect(checkout.canPlaceOrder.value).toBe(false);
    wrapper.unmount();
  });

  it("does not hold the order when the cart's subscriptions fail to load", async () => {
    state.failed.value = true;
    state.subscribedItems.value = [];

    const wrapper = mountTerms();
    await flushPromises();

    expect(checkout.canPlaceOrder.value).toBe(true);
    wrapper.unmount();
  });

  it("lets the order through without terms when the cart has no subscriptions", async () => {
    state.subscribedItems.value = [];

    const wrapper = mountTerms();
    await flushPromises();

    expect(checkout.canPlaceOrder.value).toBe(true);
    wrapper.unmount();
  });
});
