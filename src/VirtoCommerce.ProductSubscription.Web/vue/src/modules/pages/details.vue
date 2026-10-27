<template>
  <VcBlade
    :title="item.number ?? ''"
    :subtitle="$t('PRODUCT_SUBSCRIPTION.PAGES.DETAILS.SUBTITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    :modified="form.isModified.value"
    width="50%"
  >
    <VcContainer>
      <div
        v-if="item.id"
        class="ps-page"
      >
        <VcBanner
          v-if="item.status === 'PastDue'"
          variant="danger"
          icon="lucide-circle-alert"
        >
          {{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.PAST_DUE") }}
        </VcBanner>
        <VcBanner
          v-else-if="item.status === 'PendingActivation'"
          variant="info"
          icon="lucide-clock"
        >
          {{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.PENDING") }}
        </VcBanner>
        <VcBanner
          v-else-if="item.status === 'Cancelled'"
          variant="info"
          icon="lucide-ban"
        >
          {{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.CANCELLED") }}
        </VcBanner>

        <div class="ps-hero">
          <div class="ps-hero__image">
            <img
              v-if="item.imageUrl"
              :src="item.imageUrl"
              alt=""
            />
            <VcIcon
              v-else
              icon="lucide-package"
            />
          </div>
          <div class="ps-hero__text">
            <div class="ps-hero__title">{{ item.productName }}</div>
            <div class="ps-muted">
              <span dir="ltr">{{ item.sku }}</span>
              <template v-if="item.planName"> · {{ item.planName }}</template>
            </div>
          </div>
          <div class="ps-inline">
            <VcStatus :variant="statusVariant(item.status)">{{ f.status(item.status) }}</VcStatus>
          </div>
        </div>

        <div class="ps-stats">
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.NEXT_ORDER") }}</div>
            <div class="ps-stat__value">{{ item.status === "Cancelled" ? "—" : f.date(item.nextOrderDate) }}</div>
          </div>
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.FREQUENCY") }}</div>
            <div class="ps-stat__value">{{ f.interval(item.intervalUnit, item.intervalCount) }}</div>
          </div>
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.ORDERS") }}</div>
            <div class="ps-stat__value">{{ item.ordersCount ?? 0 }}</div>
          </div>
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.DISCOUNT") }}</div>
            <div class="ps-stat__value">{{ f.discount(item.discountType, item.discountValue, item.currency) }}</div>
          </div>
        </div>

        <VcCard
          v-if="panel === 'pause'"
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.DETAILS.PAUSE_TITLE')"
          icon="lucide-pause"
        >
          <div class="ps-card">
            <VcHint>{{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.PAUSE_HINT") }}</VcHint>
            <VcInput
              v-model="resumeDate"
              type="date"
              class="ps-narrow"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.RESUME_DATE')"
            />
            <div class="ps-actions">
              <VcButton
                variant="secondary"
                @click="panel = undefined"
              >
                {{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.KEEP") }}
              </VcButton>
              <VcButton @click="pause()">{{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.PAUSE_CONFIRM") }}</VcButton>
            </div>
          </div>
        </VcCard>

        <VcCard
          v-if="panel === 'cancel'"
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.DETAILS.CANCEL_TITLE')"
          icon="lucide-ban"
          variant="danger"
        >
          <div class="ps-card">
            <VcHint>{{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.CANCEL_HINT") }}</VcHint>
            <VcTextarea
              v-model="cancelReason"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.REASON')"
              :placeholder="$t('PRODUCT_SUBSCRIPTION.PAGES.DETAILS.CANCEL_PLACEHOLDER')"
            />
            <div class="ps-actions">
              <VcButton
                variant="secondary"
                @click="panel = undefined"
              >
                {{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.KEEP") }}
              </VcButton>
              <VcButton
                variant="danger"
                @click="cancel()"
              >
                {{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.CANCEL_CONFIRM") }}
              </VcButton>
            </div>
          </div>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.DETAILS.SCHEDULE')"
          icon="lucide-calendar-sync"
        >
          <div class="ps-card ps-grid">
            <VcInput
              :model-value="edit.quantity"
              type="integer"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.QUANTITY')"
              :disabled="readonly"
              required
              @update:model-value="(v) => (edit.quantity = Math.min(9999, Math.max(1, Number(v) || 1)))"
            />
            <VcSelect
              v-model="edit.interval"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.FREQUENCY')"
              :options="intervalOptions"
              :clearable="false"
              :disabled="readonly"
            />
            <VcInput
              v-model="edit.nextOrderDate"
              type="date"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.NEXT_ORDER')"
              :disabled="readonly"
            />
            <VcSelect
              v-model="edit.storedPaymentMethodId"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.PAYMENT_METHOD')"
              :options="paymentOptions"
              :placeholder="$t('PRODUCT_SUBSCRIPTION.FIELDS.NO_PAYMENT_METHOD')"
              :disabled="readonly"
              clearable
            />
          </div>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.DETAILS.CUSTOMER')"
          icon="lucide-user"
        >
          <dl class="ps-info">
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.CUSTOMER") }}</dt>
            <dd>
              {{ item.customerName }}
              <div
                v-if="item.customerEmail"
                class="ps-muted"
                dir="ltr"
              >
                {{ item.customerEmail }}
              </div>
            </dd>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.SHIPS_TO") }}</dt>
            <dd>
              {{ f.address(item.shippingAddress) || "—" }}
              <div
                v-if="item.shippingMethodCode"
                class="ps-muted"
              >
                {{ [item.shippingMethodCode, item.shippingMethodOption].filter(Boolean).join(" · ") }}
              </div>
            </dd>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.STARTED") }}</dt>
            <dd>{{ f.dateTime(item.startDate) }}</dd>
            <template v-if="item.initialOrderId">
              <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.FIRST_ORDER") }}</dt>
              <dd>
                <a
                  href="#"
                  class="ps-link"
                  @click.prevent="openOrder(openBlade, item.initialOrderId)"
                  >{{ item.initialOrderNumber || item.initialOrderId }}</a
                >
              </dd>
            </template>
            <template v-if="item.lastOrderId && item.lastOrderId !== item.initialOrderId">
              <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.LAST_ORDER") }}</dt>
              <dd>
                <a
                  href="#"
                  class="ps-link"
                  @click.prevent="openOrder(openBlade, item.lastOrderId)"
                  >{{ item.lastOrderNumber || item.lastOrderId }}</a
                >
                <span class="ps-muted"> · {{ f.dateTime(item.lastOrderDate) }}</span>
              </dd>
            </template>
            <template v-if="item.cancelledDate">
              <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.CANCELLED") }}</dt>
              <dd>
                {{ f.dateTime(item.cancelledDate) }}
                <div
                  v-if="item.cancelReason"
                  class="ps-muted"
                >
                  {{ item.cancelReason }}
                </div>
              </dd>
            </template>
            <template v-if="item.pausedUntil">
              <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.PAUSED_UNTIL") }}</dt>
              <dd>{{ f.date(item.pausedUntil) }}</dd>
            </template>
            <template v-if="consent">
              <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.CONSENT") }}</dt>
              <dd>
                {{ $t("PRODUCT_SUBSCRIPTION.FIELDS.CONSENT_VALUE", { version: consent.termsVersion ?? "—", date: f.dateTime(consent.acceptedDate) }) }}
                <div
                  v-if="consent.ipAddress"
                  class="ps-muted"
                  dir="ltr"
                >
                  {{ consent.ipAddress }}
                </div>
              </dd>
            </template>
          </dl>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.DETAILS.RENEWALS')"
          icon="lucide-truck"
        >
          <!-- a list rather than a table: the blade is often narrow, next to the subscriptions list -->
          <ul
            v-if="renewals.length"
            class="ps-rows"
          >
            <li
              v-for="r in renewals"
              :key="r.id"
            >
              <button
                type="button"
                class="ps-row"
                :class="{ 'ps-row--active': activeChildParam === r.id }"
                @click="openBlade({ name: 'ProductSubscriptionRenewalDetails', param: r.id })"
              >
                <span class="ps-row__main">
                  <span class="ps-row__title">{{ f.date(r.scheduledDate) }}</span>
                  <span class="ps-muted">{{ [r.number, r.orderNumber].filter(Boolean).join(" · ") }}</span>
                </span>
                <span class="ps-row__side">
                  <span class="ps-row__amount">{{ f.money(r.orderTotal ?? r.previewTotal, r.currency) }}</span>
                  <VcStatus :variant="statusVariant(r.state)">{{ f.status(r.state) }}</VcStatus>
                </span>
              </button>
            </li>
          </ul>
          <div
            v-else
            class="ps-card ps-muted"
          >
            {{ $t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.NO_RENEWALS") }}
          </div>
        </VcCard>
      </div>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import {
  defineBladeContext,
  notification,
  useBlade,
  useBladeForm,
  useLoading,
  usePermissions,
  usePopup,
  type IBladeToolbar,
} from "@vc-shell/framework";
import {
  VcBanner,
  VcBlade,
  VcButton,
  VcCard,
  VcContainer,
  VcHint,
  VcIcon,
  VcInput,
  VcSelect,
  VcStatus,
  VcTextarea,
} from "@vc-shell/framework/ui";
import { computed, onMounted, ref } from "vue";
import type {
  PaymentConsent,
  Renewal,
  StoredPaymentMethod,
  Subscription,
  SubscriptionChange,
  SubscriptionIntervalUnit,
  SubscriptionPlanInterval,
} from "../../api_client/virtocommerce.productsubscription";
import { fromDateInput, statusVariant, toDateInput, useFormat, useOpenOrder, useProductSubscriptionClient, withServerMessage, useAction } from "../api";

defineBlade({
  url: "/product-subscription",
  name: "ProductSubscriptionDetails",
  permissions: ["product-subscription:read"],
});

const f = useFormat();
const t = f.t;
const { param, activeChildParam, openBlade, callParent, exposeToChildren } = useBlade();
const { showConfirmation } = usePopup();
const { hasAccess } = usePermissions();
const { getApiClient } = useProductSubscriptionClient();
const openOrder = useOpenOrder();

const item = ref<Subscription>({});
const intervals = ref<SubscriptionPlanInterval[]>([]);
const paymentMethods = ref<StoredPaymentMethod[]>([]);
const renewals = ref<Renewal[]>([]);
const consent = ref<PaymentConsent>();
const panel = ref<"pause" | "cancel">();
const resumeDate = ref("");
const cancelReason = ref("");

interface Edit {
  quantity?: number;
  interval?: string;
  nextOrderDate?: string;
  storedPaymentMethodId?: string;
}
const edit = ref<Edit>({});
const form = useBladeForm({
  data: edit,
  closeConfirmMessage: computed(() => t("PRODUCT_SUBSCRIPTION.ALERTS.CLOSE_CONFIRMATION")),
});

const status = computed(() => item.value.status);
const readonly = computed(() => status.value === "Cancelled" || !hasAccess("product-subscription:update"));

const intervalOptions = computed(() =>
  intervals.value.map((i) => ({ id: f.intervalKey(i), title: f.interval(i.unit, i.count) })),
);
// a card that isn't active can't be chosen, unless it is the subscription's card already
const paymentOptions = computed(() =>
  paymentMethods.value
    .filter((m) => m.status === "Active" || m.id === item.value.storedPaymentMethodId)
    .map((m) => ({
      id: m.id,
      title: `${m.brand ?? ""} •••• ${m.last4 ?? ""} (${m.expirationMonth}/${m.expirationYear})${m.status === "Active" ? "" : " — " + m.status}`,
    })),
);

function resetEdit() {
  const s = item.value;
  edit.value = {
    quantity: s.quantity,
    interval: `${s.intervalUnit}:${s.intervalCount}`,
    nextOrderDate: toDateInput(s.nextOrderDate),
    storedPaymentMethodId: s.storedPaymentMethodId ?? undefined,
  };
  form.setBaseline();
}

const { loading: fetching, action: load } = useAction(async () => {
  const client = await getApiClient();
  const subscription = await client.getSubscription(param.value!);
  item.value = subscription;
  resetEdit();

  const current = { unit: subscription.intervalUnit, count: subscription.intervalCount };
  const [plan, methods, related, agreement] = await Promise.all([
    subscription.planId ? client.getPlan(subscription.planId).catch(() => undefined) : undefined,
    subscription.customerId ? client.getSavedPaymentMethods(subscription.customerId, subscription.storeId).catch(() => []) : [],
    client.searchRenewals({ subscriptionId: subscription.id, take: 20, sort: "ScheduledDate:desc" }),
    subscription.consentId ? client.getConsent(subscription.consentId).catch(() => undefined) : undefined,
  ]);
  const list = [...(plan?.intervals ?? [])];
  if (!list.some((i) => i.unit === current.unit && i.count === current.count)) list.unshift(current);
  intervals.value = list;
  paymentMethods.value = methods ?? [];
  renewals.value = related.results ?? [];
  consent.value = agreement;
});

const reloadParent = () => callParent("reload").catch(() => undefined);

async function run(action: () => Promise<unknown>) {
  await withServerMessage(action);
  panel.value = undefined;
  await load();
  reloadParent();
}

const { loading: saving, action: save } = useAction(async () => {
  const s = item.value;
  const e = edit.value;
  const change: SubscriptionChange = {};
  if (e.quantity !== s.quantity) change.quantity = e.quantity;
  if (e.interval && e.interval !== `${s.intervalUnit}:${s.intervalCount}`) {
    const [unit, count] = e.interval.split(":");
    change.intervalUnit = unit as SubscriptionIntervalUnit;
    change.intervalCount = Number(count);
  }
  if (e.nextOrderDate && e.nextOrderDate !== toDateInput(s.nextOrderDate)) change.nextOrderDate = fromDateInput(e.nextOrderDate);
  if ((e.storedPaymentMethodId ?? null) !== (s.storedPaymentMethodId ?? null)) change.storedPaymentMethodId = e.storedPaymentMethodId ?? "";
  await run(async () => (await getApiClient()).changeSubscription(s.id!, change));
  notification.success(t("PRODUCT_SUBSCRIPTION.ALERTS.SAVED"), { timeout: 3000 });
});

const { loading: acting, action: act } = useAction(async (action?: () => Promise<unknown>) => {
  if (action) await run(action);
});

const pause = () => act(async () => (await getApiClient()).pauseSubscription(item.value.id!, fromDateInput(resumeDate.value)));
const cancel = () =>
  act(async () =>
    (await getApiClient()).cancelSubscription(
      item.value.id!,
      cancelReason.value.trim() || t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.DEFAULT_CANCEL_REASON"),
    ),
  );

const loading = useLoading(fetching, saving, acting);
const isOneOf = (...values: string[]) => computed(() => !!status.value && values.includes(status.value));
const canChangeSchedule = isOneOf("Active", "PastDue");

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "save",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.SAVE")),
    icon: "lucide-save",
    permissions: "product-subscription:update",
    disabled: computed(() => !form.isModified.value || status.value === "Cancelled" || !edit.value.quantity),
    clickHandler: () => save(),
  },
  {
    id: "reset",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.RESET")),
    icon: "lucide-undo-2",
    disabled: computed(() => !form.isModified.value),
    clickHandler: () => resetEdit(),
  },
  {
    id: "orderNow",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.ORDER_NOW")),
    icon: "lucide-zap",
    permissions: "product-subscription:renewals:process",
    disabled: computed(() => status.value !== "Active" || form.isModified.value),
    async clickHandler() {
      if (await showConfirmation(t("PRODUCT_SUBSCRIPTION.ALERTS.ORDER_NOW"))) {
        await act(async () => (await getApiClient()).orderSubscriptionNow(item.value.id!));
      }
    },
  },
  {
    id: "skip",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.SKIP")),
    icon: "lucide-skip-forward",
    permissions: "product-subscription:update",
    disabled: computed(() => !canChangeSchedule.value || form.isModified.value),
    clickHandler: () => act(async () => (await getApiClient()).skipSubscription(item.value.id!)),
  },
  {
    id: "pause",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.PAUSE")),
    icon: "lucide-pause",
    permissions: "product-subscription:update",
    disabled: computed(() => !canChangeSchedule.value || panel.value === "pause"),
    clickHandler: () => {
      resumeDate.value = "";
      panel.value = "pause";
    },
  },
  {
    id: "resume",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.RESUME")),
    icon: "lucide-play",
    permissions: "product-subscription:update",
    disabled: computed(() => status.value !== "Paused"),
    clickHandler: () => act(async () => (await getApiClient()).resumeSubscription(item.value.id!)),
  },
  {
    id: "cancel",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.CANCEL_SUBSCRIPTION")),
    icon: "lucide-ban",
    permissions: "product-subscription:update",
    disabled: computed(() => !status.value || status.value === "Cancelled" || panel.value === "cancel"),
    clickHandler: () => {
      cancelReason.value = "";
      panel.value = "cancel";
    },
  },
  { id: "refresh", title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.REFRESH")), icon: "lucide-refresh-cw", clickHandler: () => load() },
]);

// the subscription, for widgets other modules may add to this blade
defineBladeContext({ item });

onMounted(load);

exposeToChildren({
  async reload() {
    await load();
    reloadParent();
  },
});
</script>
