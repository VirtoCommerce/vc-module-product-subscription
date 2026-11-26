import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import SubscriptionCardModal from "./subscription-card-modal.vue";

const h = vi.hoisted(() => ({
  loadCards: vi.fn(),
}));

vi.mock("vue-i18n", () => ({
  useI18n: () => ({ t: (key: string) => key }),
}));

// The host's saved cards, as the modal reads them: the active ones, and whether the last load finished or failed.
vi.mock("@vc-frontend/core", async (importOriginal) => {
  const { computed, ref } = await import("vue");
  const methods = ref<{ status: string; isExpired: boolean }[]>([]);
  const loaded = ref(false);
  const failed = ref(false);
  async function fetch() {
    failed.value = false;
    try {
      methods.value = await h.loadCards();
      loaded.value = true;
    } catch {
      failed.value = true;
    }
  }
  const activeMethods = computed(() => methods.value.filter((x) => x.status === "Active" && !x.isExpired));
  return {
    ...(await importOriginal<Record<string, unknown>>()),
    useSavedPaymentMethods: () => ({ activeMethods, loaded, failed, fetch }),
  };
});

const card = {
  id: "card-1",
  brand: "Visa",
  last4: "4242",
  expirationMonth: 12,
  expirationYear: 2030,
  status: "Active",
  isExpired: false,
};

function mountModal() {
  return mount(SubscriptionCardModal, {
    props: { products: ["Coffee"], currentCardId: "card-1", onSave: vi.fn() },
    global: {
      stubs: {
        VcModal: { template: "<div><slot /><slot name='actions' :close='() => {}' /></div>" },
        VcAlert: { template: "<div><slot /></div>" },
        VcButton: { template: "<button><slot /></button>" },
        VcSelect: { name: "VcSelect", props: ["modelValue", "items"], template: "<div />" },
        VcList: true,
        VcListItem: true,
        RouterLink: true,
      },
      mocks: { $t: (key: string) => key },
    },
  });
}

describe("SubscriptionCardModal", () => {
  enableAutoUnmount(afterEach);

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a load error with a retry instead of the no-cards message when the cards fail to load", async () => {
    h.loadCards.mockRejectedValueOnce(new Error("network"));
    const wrapper = mountModal();
    await flushPromises();

    expect(wrapper.find("[data-test-id='subscription-card-modal-load-error']").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("product_subscriptions.deliveries.no_cards");
  });

  it("lists the cards and preselects the one in use after a retry", async () => {
    h.loadCards.mockRejectedValueOnce(new Error("network")).mockResolvedValueOnce([card]);
    const wrapper = mountModal();
    await flushPromises();

    await wrapper.get("[data-test-id='subscription-card-modal-retry-button']").trigger("click");
    await flushPromises();

    expect(wrapper.find("[data-test-id='subscription-card-modal-load-error']").exists()).toBe(false);
    const select = wrapper.getComponent({ name: "VcSelect" });
    expect(select.props("items")).toEqual([expect.objectContaining({ id: "card-1" })]);
    expect(select.props("modelValue")).toBe("card-1");
  });

  it("says there are no saved cards when the customer has none", async () => {
    h.loadCards.mockResolvedValueOnce([]);
    const wrapper = mountModal();
    await flushPromises();

    expect(wrapper.text()).toContain("product_subscriptions.deliveries.no_cards");
    expect(wrapper.find("[data-test-id='subscription-card-modal-load-error']").exists()).toBe(false);
  });
});
