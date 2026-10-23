import { useApiClient, useAsync } from "@vc-shell/framework";
import { getCurrentInstance } from "vue";
import { useI18n } from "vue-i18n";
import {
  ProductSubscriptionClient,
  SubscriptionDiscountType,
  type Address,
  type SubscriptionPlanInterval,
} from "../api_client/virtocommerce.productsubscription";

export const useProductSubscriptionClient = () => useApiClient(ProductSubscriptionClient);

/**
 * useAsync whose action doesn't reject: useAsync has shown the error already, and a rejection left to a toolbar button
 * or a click handler would reach the global error handler and show it a second time.
 */
export function useAction<Payload = void>(fn: (payload?: Payload) => Promise<unknown>) {
  const { loading, action } = useAsync<Payload, unknown>(fn);
  return { loading, action: (payload?: Payload) => action(payload).then(() => undefined, () => undefined) };
}

// Same-origin fetch for endpoints the generated client doesn't cover (other modules, untyped results);
// cookie auth works because the host is same-origin.
async function http<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    credentials: "same-origin",
    headers: body === undefined ? { Accept: "application/json" } : { Accept: "application/json", "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  if (!response.ok) throw new Error(messageOf(text) || `${method} ${url}: ${response.status}`);
  return (text ? JSON.parse(text) : undefined) as T;
}

// the text of a platform error body: { message } from the module's own validation, ProblemDetails otherwise
function messageOf(body?: string) {
  if (!body) return undefined;
  try {
    const json = JSON.parse(body);
    if (typeof json === "string") return json;
    const errors = json?.errors && typeof json.errors === "object" ? Object.values(json.errors).flat().join(" ") : undefined;
    return json?.message || json?.detail || errors || json?.title || undefined;
  } catch {
    return body.length < 300 ? body : undefined;
  }
}

/** Runs a call of the generated client; a rejected call fails with the server's message ("sold in packs of 7"),
 * not the client's generic "An unexpected server error occurred." */
export async function withServerMessage<T>(action: () => Promise<T>): Promise<T> {
  try {
    return await action();
  } catch (e) {
    const response = (e as { response?: string })?.response;
    throw new Error(messageOf(response) || (e as Error)?.message || String(e));
  }
}

export interface SearchResult<T> {
  totalCount?: number;
  results?: T[];
}

export interface Store {
  id: string;
  name?: string;
}

export interface CatalogEntry {
  id: string;
  type?: string;
  name?: string;
  code?: string;
  imageUrl?: string;
  path?: string[];
}

export const searchStores = (keyword?: string, skip = 0, ids?: string[]) =>
  http<SearchResult<Store>>("POST", "/api/stores/search", { keyword, objectIds: ids, skip, take: ids?.length || 50 });

// the generated client types the result as void: the endpoint answers { processed }
export const runScheduler = () => http<{ processed?: number }>("POST", "/api/product-subscription/scheduler/run");

/** Products and categories of every catalog, by keyword: replaces the Catalog module's AngularJS "select items" blade */
export async function searchCatalog(keyword: string | undefined, types: string[], skip: number, take: number) {
  const result = await http<{ listEntries?: CatalogEntry[]; totalCount?: number }>("POST", "/api/catalog/listentries", {
    keyword,
    objectTypes: types,
    searchInChildren: true,
    skip,
    take,
  });
  return { results: result.listEntries ?? [], totalCount: result.totalCount ?? 0 };
}

/** Display names of the plan's products and categories ("Name (CODE)") */
export async function catalogNames(productIds: string[], categoryIds: string[]) {
  const names: Record<string, string> = {};
  const [products, categories] = await Promise.all([
    productIds.length
      ? http<{ id: string; name?: string; code?: string }[]>("POST", "/api/catalog/products/plenty?respGroup=ItemInfo", productIds).catch(() => [])
      : [],
    categoryIds.length
      ? http<{ id: string; name?: string }[]>("POST", "/api/catalog/categories/plenty?respGroup=Info", categoryIds).catch(() => [])
      : [],
  ]);
  (products ?? []).forEach((p) => (names[p.id] = p.code ? `${p.name} (${p.code})` : (p.name ?? p.id)));
  (categories ?? []).forEach((c) => (names[c.id] = c.name ?? c.id));
  return names;
}

export const subscriptionStatuses = ["PendingActivation", "Active", "Paused", "PastDue", "Cancelled"];
export const renewalStates = [
  "Scheduled",
  "Notified",
  "ReviewRequired",
  "Ordered",
  "PaymentActionRequired",
  "RetryScheduled",
  "Authorized",
  "Completed",
  "Failed",
  "Skipped",
  "Cancelled",
];

type Variant = "success" | "warning" | "danger" | "info" | "primary";

// VcStatus variant of a subscription status or a renewal state
const variants: Record<string, Variant> = {
  PendingActivation: "info",
  Active: "success",
  Paused: "warning",
  PastDue: "danger",
  Cancelled: "info",
  Scheduled: "info",
  Notified: "info",
  ReviewRequired: "warning",
  Ordered: "primary",
  PaymentActionRequired: "warning",
  RetryScheduled: "warning",
  Authorized: "success",
  Completed: "success",
  Failed: "danger",
  Skipped: "info",
};
export const statusVariant = (status?: string): Variant => (status && variants[status]) || "info";

/** Labels and formats shared by the blades, in the user's language */
export function useFormat() {
  const { t, te, locale } = useI18n({ useScope: "global" });

  const status = (value?: string) => (value && te(`PRODUCT_SUBSCRIPTION.STATUSES.${value}`) ? t(`PRODUCT_SUBSCRIPTION.STATUSES.${value}`) : (value ?? ""));

  const interval = (unit?: string, count?: number) =>
    unit ? t(`PRODUCT_SUBSCRIPTION.INTERVALS.${unit.toUpperCase()}`, { count: count ?? 1 }, count ?? 1) : "";

  const intervalKey = (i: SubscriptionPlanInterval) => `${i.unit}:${i.count}`;

  const money = (value?: number | null, currency?: string) => {
    if (value === undefined || value === null) return "—";
    try {
      return currency
        ? new Intl.NumberFormat(locale.value, { style: "currency", currency }).format(value)
        : new Intl.NumberFormat(locale.value, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
    } catch {
      return `${value.toFixed(2)} ${currency ?? ""}`.trim();
    }
  };

  const discount = (type?: string, value?: number, currency?: string) => {
    if (!value || type === SubscriptionDiscountType.None) return "—";
    return type === SubscriptionDiscountType.Percent
      ? `${new Intl.NumberFormat(locale.value).format(value)}%`
      : money(value, currency);
  };

  const format = (value: Date | string | undefined | null, options: Intl.DateTimeFormatOptions) => {
    if (!value) return "—";
    const date = value instanceof Date ? value : new Date(value);
    return isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat(locale.value, options).format(date);
  };
  const date = (value?: Date | string | null) => format(value, { dateStyle: "medium" });
  const dateTime = (value?: Date | string | null) => format(value, { dateStyle: "medium", timeStyle: "short" });

  const address = (a?: Address) => {
    if (!a) return "";
    const name = [a.firstName, a.lastName].filter(Boolean).join(" ");
    const region = [a.regionName || a.regionId, a.postalCode].filter(Boolean).join(" ");
    return [name, a.line1, a.line2, a.city, region, a.countryCode].filter(Boolean).join(", ");
  };

  return { t, status, interval, intervalKey, money, discount, date, dateTime, address };
}

/** yyyy-mm-dd of a date in local time: the value of a date input */
export function toDateInput(value?: Date | string | null) {
  if (!value) return "";
  const date = value instanceof Date ? value : new Date(value);
  if (isNaN(date.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** A date input's value (yyyy-mm-dd, or a Date some inputs emit) at local midnight */
export function fromDateInput(value: unknown): Date | undefined {
  if (!value) return undefined;
  if (value instanceof Date) return isNaN(value.getTime()) ? undefined : value;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value));
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : undefined;
}

interface LegacyContext {
  injector: { get<T = any>(name: string): T };
  showBlade: (blade: Record<string, unknown>) => void;
}

interface ShellNavigation {
  openLegacy(options: { url: string; parent?: string; open: (context: LegacyContext) => void }): Promise<void>;
}

/**
 * Opens an order: the Orders module's Vue blade when its plugin is installed, otherwise its AngularJS blade in the
 * shell's embedded admin, under the orders list, as the AngularJS subscription blades did.
 */
export function useOpenOrder() {
  const shell = getCurrentInstance()?.appContext.config.globalProperties.$globalSearch as ShellNavigation | undefined;

  return async (openBlade: (event: { name: string; param?: string }) => Promise<void>, orderId?: string) => {
    if (!orderId) return;
    try {
      await openBlade({ name: "OrdersDetails", param: orderId });
      return;
    } catch {
      // no Vue order blade: the AngularJS one
    }
    await shell?.openLegacy({
      url: "/workspace/orders",
      parent: "orders",
      open: ({ injector, showBlade }) => {
        const operation = injector.get("virtoCommerce.orderModule.knownOperations").getOperation("CustomerOrder");
        if (!operation) return;
        showBlade({
          ...operation.detailBlade,
          id: "productSubscriptionOrder",
          customerOrder: { id: orderId, operationType: "CustomerOrder" },
        });
      },
    });
  };
}
