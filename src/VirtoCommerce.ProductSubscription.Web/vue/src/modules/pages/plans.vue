<template>
  <VcBlade
    :title="$t('PRODUCT_SUBSCRIPTION.PAGES.PLANS.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcDataTable
      v-model:search-value="searchValue"
      :items="items"
      :total-count="items.length"
      :searchable="true"
      state-key="PRODUCT_SUBSCRIPTION_PLANS"
      :active-item-id="activeChildParam"
      :show-all-columns="!activeChildParam"
      :empty-state="{ icon: 'lucide-tags', title: $t('PRODUCT_SUBSCRIPTION.PAGES.PLANS.EMPTY') }"
      @row-click="(e: { data: SubscriptionPlan }) => openPlan(e.data.id)"
    >
      <VcColumn
        id="name"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.NAME')"
        always-visible
        mobile-role="title"
      >
        <template #body="{ data: p }">
          <div class="ps-product-cell">
            <span class="ps-product-cell__name">{{ p.name }}</span>
            <span
              v-if="p.priority"
              class="ps-muted"
              >{{ $t("PRODUCT_SUBSCRIPTION.FIELDS.PRIORITY") }} {{ p.priority }}</span
            >
          </div>
        </template>
      </VcColumn>
      <VcColumn
        id="isActive"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.STATUS')"
        width="120px"
        always-visible
        mobile-role="status"
      >
        <template #body="{ data: p }">
          <div class="ps-inline">
            <VcStatus :variant="p.isActive ? 'success' : 'info'">{{ f.status(p.isActive ? "Active" : "Inactive") }}</VcStatus>
          </div>
        </template>
      </VcColumn>
      <VcColumn
        id="storeId"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.STORE')"
        width="140px"
        mobile-role="field"
      >
        <template #body="{ data: p }">{{ storeNames[p.storeId] ?? p.storeId }}</template>
      </VcColumn>
      <VcColumn
        id="discountValue"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.DISCOUNT')"
        width="110px"
        mobile-role="field"
      >
        <template #body="{ data: p }">{{ f.discount(p.discountType, p.discountValue) }}</template>
      </VcColumn>
      <VcColumn
        id="intervals"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.FREQUENCIES')"
        mobile-role="field"
      >
        <template #body="{ data: p }">{{ (p.intervals ?? []).map((i: SubscriptionPlanInterval) => f.interval(i.unit, i.count)).join(", ") }}</template>
      </VcColumn>
      <VcColumn
        id="appliesTo"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.APPLIES_TO')"
        mobile-role="field"
      >
        <template #body="{ data: p }">{{ appliesTo(p) }}</template>
      </VcColumn>
    </VcDataTable>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, type IBladeToolbar } from "@vc-shell/framework";
import { VcBlade, VcColumn, VcDataTable, VcStatus } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import type { SubscriptionPlan, SubscriptionPlanInterval } from "../../api_client/virtocommerce.productsubscription";
import { searchStores, useFormat, useProductSubscriptionClient, useAction } from "../api";

defineBlade({
  url: "/product-subscription-plans",
  name: "ProductSubscriptionPlans",
  permissions: ["product-subscription:plans:manage"],
});

const f = useFormat();
const t = f.t;
const { activeChildParam, openBlade, exposeToChildren } = useBlade();
const { getApiClient } = useProductSubscriptionClient();

const plans = ref<SubscriptionPlan[]>([]);
const storeNames = ref<Record<string, string>>({});
const searchValue = ref("");

// plans are few (the AngularJS list read up to 200 at once): filtered here
const items = computed(() => {
  const keyword = searchValue.value.trim().toLowerCase();
  return keyword ? plans.value.filter((p) => `${p.name} ${p.description ?? ""}`.toLowerCase().includes(keyword)) : plans.value;
});

const appliesTo = (p: SubscriptionPlan) =>
  t("PRODUCT_SUBSCRIPTION.FIELDS.APPLIES_TO_VALUE", {
    products: t("PRODUCT_SUBSCRIPTION.FIELDS.PRODUCTS_COUNT", { count: p.productIds?.length ?? 0 }, p.productIds?.length ?? 0),
    categories: t("PRODUCT_SUBSCRIPTION.FIELDS.CATEGORIES_COUNT", { count: p.categoryIds?.length ?? 0 }, p.categoryIds?.length ?? 0),
  });

const { loading, action: load } = useAction(async () => {
  const [result, stores] = await Promise.all([
    (await getApiClient()).searchPlans({ take: 200, sort: "priority:desc;name" }),
    searchStores().catch(() => ({ results: [] })),
  ]);
  plans.value = result.results ?? [];
  storeNames.value = Object.fromEntries((stores.results ?? []).map((s) => [s.id, s.name ?? s.id]));
});

const openPlan = (id?: string) => openBlade({ name: "ProductSubscriptionPlanDetails", param: id });

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "add",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.ADD")),
    icon: "lucide-plus",
    permissions: "product-subscription:plans:manage",
    clickHandler: () => openPlan(),
  },
  { id: "refresh", title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.REFRESH")), icon: "lucide-refresh-cw", clickHandler: () => load() },
]);

load();
exposeToChildren({ reload: () => load() });
</script>
