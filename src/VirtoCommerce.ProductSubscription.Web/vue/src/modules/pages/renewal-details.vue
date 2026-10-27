<template>
  <VcBlade
    :title="item.number ?? ''"
    :subtitle="$t('PRODUCT_SUBSCRIPTION.PAGES.RENEWAL.SUBTITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcContainer>
      <div
        v-if="item.id"
        class="ps-page"
      >
        <VcBanner
          v-if="item.reviewReason && item.state === 'ReviewRequired'"
          variant="warning"
          icon="lucide-triangle-alert"
        >
          {{ item.reviewReason }}
        </VcBanner>
        <VcBanner
          v-if="item.lastErrorMessage && item.state !== 'Completed'"
          variant="danger"
          icon="lucide-circle-alert"
        >
          {{ item.lastErrorMessage }}
        </VcBanner>

        <div class="ps-stats">
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.STATE") }}</div>
            <div class="ps-stat__value ps-inline">
              <VcStatus :variant="statusVariant(item.state)">{{ f.status(item.state) }}</VcStatus>
            </div>
          </div>
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.DELIVERY_DATE") }}</div>
            <div class="ps-stat__value">{{ f.date(item.scheduledDate) }}</div>
          </div>
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.CUTOFF") }}</div>
            <div class="ps-stat__value">{{ f.dateTime(item.cutoffDate) }}</div>
          </div>
          <div class="ps-stat">
            <div class="ps-stat__label">{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.TOTAL") }}</div>
            <div class="ps-stat__value">{{ f.money(item.orderTotal ?? item.previewTotal, item.currency) }}</div>
          </div>
        </div>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.RENEWAL.PAYMENT')"
          icon="lucide-credit-card"
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
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.ORDER") }}</dt>
            <dd>
              <a
                v-if="item.orderId"
                href="#"
                class="ps-link"
                @click.prevent="openOrder(openBlade, item.orderId)"
                >{{ item.orderNumber || item.orderId }}</a
              >
              <span
                v-else
                class="ps-muted"
                >{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.NOT_PLACED") }}</span
              >
            </dd>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.REMINDER_TOTAL") }}</dt>
            <dd>{{ f.money(item.notifiedTotal, item.currency) }}</dd>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.DISCOUNT_TOTAL") }}</dt>
            <dd>{{ f.money(item.discountTotal, item.currency) }}</dd>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.SHIPPING_TOTAL") }}</dt>
            <dd>{{ f.money(item.shippingTotal, item.currency) }}</dd>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.TAX_TOTAL") }}</dt>
            <dd>{{ f.money(item.taxTotal, item.currency) }}</dd>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.PAYMENT") }}</dt>
            <dd>
              {{ item.paymentMethodCode || "—" }}
              <div
                v-if="item.attemptCount"
                class="ps-muted"
              >
                {{ $t("PRODUCT_SUBSCRIPTION.FIELDS.ATTEMPTS", { count: item.attemptCount }, item.attemptCount) }}
                <template v-if="item.lastOutcome"> · {{ item.lastOutcome }}</template>
              </div>
              <div
                v-if="item.nextAttemptDate"
                class="ps-muted"
              >
                {{ $t("PRODUCT_SUBSCRIPTION.FIELDS.NEXT_ATTEMPT", { date: f.dateTime(item.nextAttemptDate) }) }}
              </div>
            </dd>
            <template v-if="item.authorizedDate">
              <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.AUTHORIZED") }}</dt>
              <dd>
                {{ f.dateTime(item.authorizedDate) }}
                <span
                  v-if="item.authorizationExpiresDate"
                  class="ps-muted"
                >
                  · {{ $t("PRODUCT_SUBSCRIPTION.FIELDS.AUTHORIZATION_EXPIRES", { date: f.dateTime(item.authorizationExpiresDate) }) }}
                </span>
              </dd>
            </template>
            <template v-if="item.capturedDate">
              <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.CAPTURED") }}</dt>
              <dd>{{ f.dateTime(item.capturedDate) }}</dd>
            </template>
            <dt>{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.IDEMPOTENCY_KEY") }}</dt>
            <dd
              class="ps-muted ps-code"
              dir="ltr"
            >
              {{ item.idempotencyKey || "—" }}
            </dd>
          </dl>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.RENEWAL.ITEMS')"
          icon="lucide-package"
        >
          <ul class="ps-rows">
            <li
              v-for="line in item.items ?? []"
              :key="line.id"
            >
              <button
                type="button"
                class="ps-row"
                @click="openSubscription(line)"
              >
                <span class="ps-row__image">
                  <img
                    v-if="line.imageUrl"
                    :src="line.imageUrl"
                    alt=""
                  />
                  <VcIcon
                    v-else
                    icon="lucide-package"
                  />
                </span>
                <span class="ps-row__main ps-row__grow">
                  <span class="ps-row__title">{{ line.productName }}</span>
                  <span class="ps-muted">
                    <span dir="ltr">{{ line.sku }}</span> ·
                    {{ line.quantity }} × {{ f.money(line.listPrice, item.currency) }}
                    <template v-if="line.discountAmount">
                      · {{ $t("PRODUCT_SUBSCRIPTION.FIELDS.ITEM_DISCOUNT") }} {{ f.money(line.discountAmount, item.currency) }}
                    </template>
                  </span>
                  <span
                    v-if="!line.isAvailable || line.isExcluded"
                    class="ps-inline"
                  >
                    <VcStatus variant="warning">{{ line.isExcluded ? $t("PRODUCT_SUBSCRIPTION.FIELDS.EXCLUDED") : line.message }}</VcStatus>
                  </span>
                </span>
                <span class="ps-row__side">
                  <span class="ps-row__amount">{{ f.money(line.total, item.currency) }}</span>
                </span>
              </button>
            </li>
          </ul>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.RENEWAL.HISTORY')"
          icon="lucide-history"
        >
          <ol class="ps-timeline">
            <li
              v-for="e in events"
              :key="e.id"
              :class="{ 'ps-timeline__item--error': e.eventType === 'Error' }"
              class="ps-timeline__item"
            >
              <div class="ps-muted">
                {{ f.dateTime(e.eventDate) }} · {{ e.eventType }}<template v-if="e.state"> · {{ f.status(e.state) }}</template>
              </div>
              <div>{{ e.message }}</div>
              <div
                v-if="e.details"
                class="ps-timeline__details"
              >
                {{ e.details }}
              </div>
            </li>
          </ol>
        </VcCard>
      </div>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, useLoading, usePopup, type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcCard, VcContainer, VcIcon, VcStatus } from "@vc-shell/framework/ui";
import { computed, onMounted, ref } from "vue";
import type { Renewal, RenewalItem } from "../../api_client/virtocommerce.productsubscription";
import { statusVariant, useFormat, useOpenOrder, useProductSubscriptionClient, withServerMessage, useAction } from "../api";

defineBlade({
  url: "/product-subscription-renewal",
  name: "ProductSubscriptionRenewalDetails",
  permissions: ["product-subscription:read"],
});

const f = useFormat();
const t = f.t;
const { param, openBlade, callParent, exposeToChildren } = useBlade();
const { showConfirmation } = usePopup();
const { getApiClient } = useProductSubscriptionClient();
const openOrder = useOpenOrder();

const item = ref<Renewal>({});
const events = computed(() =>
  [...(item.value.events ?? [])].sort((a, b) => new Date(b.eventDate ?? 0).getTime() - new Date(a.eventDate ?? 0).getTime()),
);

const { loading: fetching, action: load } = useAction(async () => {
  item.value = await (await getApiClient()).getRenewal(param.value!);
});

const reloadParent = () => callParent("reload").catch(() => undefined);

const { loading: acting, action: act } = useAction(async (action?: () => Promise<unknown>) => {
  if (!action) return;
  await withServerMessage(action);
  await load();
  reloadParent();
});

const loading = useLoading(fetching, acting);
const state = computed(() => item.value.state ?? "");
const beforeOrder = computed(() => ["Scheduled", "Notified", "ReviewRequired"].includes(state.value));
const client = async () => getApiClient();

function openSubscription(line: RenewalItem) {
  if (line.subscriptionId) openBlade({ name: "ProductSubscriptionDetails", param: line.subscriptionId });
}

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "processNow",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.PROCESS_NOW")),
    icon: "lucide-fast-forward",
    permissions: "product-subscription:renewals:process",
    disabled: computed(() => !state.value || ["Completed", "Failed", "Skipped", "Cancelled"].includes(state.value)),
    async clickHandler() {
      if (await showConfirmation(t("PRODUCT_SUBSCRIPTION.ALERTS.PROCESS_NOW"))) {
        await act(async () => (await client()).processRenewal(item.value.id!));
      }
    },
  },
  {
    id: "accept",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.ACCEPT_CHANGES")),
    icon: "lucide-check",
    permissions: "product-subscription:renewals:process",
    disabled: computed(() => state.value !== "ReviewRequired"),
    clickHandler: () => act(async () => (await client()).acceptRenewal(item.value.id!)),
  },
  {
    id: "retryPayment",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.RETRY_PAYMENT")),
    icon: "lucide-rotate-cw",
    permissions: "product-subscription:renewals:process",
    disabled: computed(() => !["Failed", "RetryScheduled", "PaymentActionRequired"].includes(state.value)),
    clickHandler: () => act(async () => (await client()).retryRenewalPayment(item.value.id!)),
  },
  {
    id: "skip",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.SKIP_RENEWAL")),
    icon: "lucide-skip-forward",
    permissions: "product-subscription:renewals:process",
    disabled: computed(() => !beforeOrder.value),
    clickHandler: () => act(async () => (await client()).skipRenewal(item.value.id!)),
  },
  {
    id: "cancel",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.CANCEL_RENEWAL")),
    icon: "lucide-ban",
    permissions: "product-subscription:renewals:process",
    disabled: computed(() => !beforeOrder.value),
    async clickHandler() {
      if (await showConfirmation(t("PRODUCT_SUBSCRIPTION.ALERTS.CANCEL_RENEWAL"))) {
        await act(async () => (await client()).cancelRenewal(item.value.id!, t("PRODUCT_SUBSCRIPTION.PAGES.DETAILS.DEFAULT_CANCEL_REASON")));
      }
    },
  },
  {
    id: "openOrder",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.OPEN_ORDER")),
    icon: "lucide-file-text",
    disabled: computed(() => !item.value.orderId),
    clickHandler: () => openOrder(openBlade, item.value.orderId),
  },
  { id: "refresh", title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.REFRESH")), icon: "lucide-refresh-cw", clickHandler: () => load() },
]);

onMounted(load);

exposeToChildren({
  async reload() {
    await load();
    reloadParent();
  },
});
</script>
