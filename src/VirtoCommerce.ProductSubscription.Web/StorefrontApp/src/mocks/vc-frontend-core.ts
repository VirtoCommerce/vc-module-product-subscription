// Test-only resolution target for "@vc-frontend/core" (see the alias in vitest.config.ts).
// Mocking the facade is wholesale, so this carries working defaults and specs SPREAD it:
//
//   vi.mock("@vc-frontend/core", async (importOriginal) => ({
//     ...(await importOriginal<Record<string, unknown>>()),
//     globals: { storeId: "test-store" },
//   }));
//
// Add the symbols your plugin uses.
export const SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT = { suppressErrorNotifications: true };

export const Logger = { error: () => {}, warn: () => {}, info: () => {}, debug: () => {} };

export const globals = {
  storeId: "test-store",
  cultureName: "en-US",
  currencyCode: "USD",
  i18n: undefined,
  router: undefined,
};

export const useUser = () => ({ checkPermissions: () => true, isAuthenticated: { value: true } });
export const useModuleSettings = () => ({ isEnabled: () => true, getModuleSettings: () => undefined });
export const useModal = () => ({ openModal: () => () => {}, closeModal: () => {} });
export const useNotifications = () => ({ success: () => {}, error: () => {}, warning: () => {}, info: () => {} });
export const useNavigations = () => ({ mergeMenuSchema: () => {}, registerAccountSection: () => {} });
export const useExtensionRegistry = () => ({ register: () => {}, registerContribution: () => {} });
export const usePageHead = () => {};
export const registerLocaleLoader = () => {};
export const registerCacheTypePolicies = () => {};

// Product subscriptions: the host symbols this plugin uses, with working defaults.
export const DEFAULT_NOTIFICATION_DURATION = 15000;
export const ROUTES = {
  ACCOUNT: { NAME: "Account" },
  ORDER_DETAILS: { NAME: "OrderDetails" },
  PAYMENT_METHODS: { NAME: "PaymentMethods" },
};
export const graphqlClient = { query: async () => ({ data: {} }), mutate: async () => ({ data: {} }) };
export const getProductRoute = (id: string) => ({ name: "Product", params: { productId: id } });
export const prepareLineItem = <T>(item: T) => item;
export const toLocalDateOnly = (date: Date) =>
  `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, "0")}-${`${date.getDate()}`.padStart(2, "0")}`;
export const savedCardLabel = (brand: string | null | undefined, last4: string | null | undefined, fallback: string) =>
  `${brand || fallback} •••• ${last4 ?? "????"}`;
export const savedCardExpiry = (month: string | number | null | undefined, year: string | number | null | undefined) =>
  month && year ? `${month}/${year}` : undefined;
export const useShortCart = () => ({ cart: { value: undefined } });
export const useCurrentCart = () => ({
  cart: { value: undefined },
  availablePaymentMethods: { value: [] },
  refetch: async () => {},
});
export const useOrderPlacement = () => ({ placingOrder: { value: false } });
export const useCheckoutGuards = () => ({ registerGuard: () => () => {}, unregisterGuard: () => {} });
export const useCardSavingRequirement = () => ({ require: () => {}, read: () => undefined, clear: () => {} });
