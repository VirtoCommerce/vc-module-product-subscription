<template>
  <VcDataTable
    v-model:search-value="searchValue"
    v-model:sort-field="sortField"
    v-model:sort-order="sortOrder"
    :items="items"
    :total-count="totalCount"
    :pagination="pagination"
    :searchable="true"
    :search-placeholder="$t('PRODUCT_SUBSCRIPTION.PAGES.LIST.SEARCH')"
    :state-key="stateKey"
    :active-item-id="activeItemId"
    :show-all-columns="!activeItemId"
    :global-filters="globalFilters"
    :empty-state="{ icon: 'lucide-repeat', title: $t('PRODUCT_SUBSCRIPTION.PAGES.LIST.EMPTY') }"
    @row-click="(e: { data: Subscription }) => emit('open', e.data)"
    @pagination-click="pagination.goToPage"
    @filter="onFilter"
  >
    <VcColumn
      id="number"
      :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.NUMBER')"
      width="160px"
      sortable
      always-visible
      mobile-role="title"
    />
    <VcColumn
      id="status"
      :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.STATUS')"
      width="150px"
      sortable
      mobile-role="status"
    >
      <template #body="{ data: s }">
        <div class="ps-inline">
          <VcStatus :variant="statusVariant(s.status)">{{ f.status(s.status) }}</VcStatus>
        </div>
      </template>
    </VcColumn>
    <VcColumn
      id="productName"
      :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.PRODUCT')"
      sortable
      always-visible
      mobile-role="field"
    >
      <template #body="{ data: s }">
        <div class="ps-product-cell">
          <span class="ps-product-cell__name">{{ s.productName }}</span>
          <span class="ps-muted">{{ s.sku }}</span>
        </div>
      </template>
    </VcColumn>
    <VcColumn
      id="quantity"
      :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.QUANTITY')"
      type="number"
      align="end"
      width="110px"
      sortable
    >
      <template #body="{ data: s }">
        <span class="vc-table-cell-number">{{ s.quantity }}</span>
      </template>
    </VcColumn>
    <VcColumn
      id="intervalCount"
      :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.FREQUENCY')"
      width="150px"
      mobile-role="field"
    >
      <template #body="{ data: s }">{{ f.interval(s.intervalUnit, s.intervalCount) }}</template>
    </VcColumn>
    <VcColumn
      v-if="!customerId"
      id="customerName"
      :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.CUSTOMER')"
      sortable
      mobile-role="field"
    />
    <VcColumn
      id="nextOrderDate"
      :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.NEXT_ORDER')"
      width="130px"
      sortable
      always-visible
      mobile-role="field"
    >
      <template #body="{ data: s }">{{ s.status === "Cancelled" ? "—" : f.date(s.nextOrderDate) }}</template>
    </VcColumn>
  </VcDataTable>
</template>

<script setup lang="ts">
import { useDataTablePagination, useDataTableSort, useFunctions, useLatestRequest, useTableSearch } from "@vc-shell/framework";
import { VcColumn, VcDataTable, VcStatus } from "@vc-shell/framework/ui";
import { computed, ref, watch } from "vue";
import type { Subscription } from "../../api_client/virtocommerce.productsubscription";
import { statusVariant, subscriptionStatuses, useFormat, useProductSubscriptionClient, useAction } from "../api";

const props = defineProps<{
  customerId?: string;
  activeItemId?: string;
  /** a change of it reloads the table */
  reloadKey?: number;
}>();
const emit = defineEmits<{ open: [subscription: Subscription]; loading: [value: boolean] }>();

const PAGE_SIZE = 20;
const f = useFormat();
const { debounce } = useFunctions();
const { getApiClient } = useProductSubscriptionClient();
const stateKey = props.customerId ? "PRODUCT_SUBSCRIPTIONS_CUSTOMER" : "PRODUCT_SUBSCRIPTIONS";

const items = ref<Subscription[]>([]);
const totalCount = ref(0);
const statuses = ref<string[]>();

const { sortField, sortOrder, sortExpression } = useDataTableSort({ stateKey, initialField: "nextOrderDate", initialDirection: "ASC" });
const { searchValue } = useTableSearch({ stateKey });
const pagination = useDataTablePagination({ stateKey, pageSize: PAGE_SIZE, totalCount });
const search = useLatestRequest();

const { loading, action: load } = useAction(async () => {
  const result = await search.latest(
    (await getApiClient()).searchSubscriptions({
      keyword: searchValue.value || undefined,
      statuses: statuses.value,
      customerId: props.customerId,
      sort: sortExpression.value,
      skip: pagination.skip,
      take: PAGE_SIZE,
    }),
  );
  if (!result) return;
  items.value = result.results ?? [];
  totalCount.value = result.totalCount ?? 0;
});
watch(loading, (value) => emit("loading", value));

const globalFilters = computed(() => [
  {
    id: "status",
    label: f.t("PRODUCT_SUBSCRIPTION.FIELDS.STATUS"),
    filter: { options: subscriptionStatuses.map((s) => ({ value: s, label: f.status(s) })) },
  },
]);

function onFilter(event: { filters: Record<string, unknown> }) {
  const status = event.filters.status as string | string[] | undefined;
  statuses.value = status && status.length ? ([] as string[]).concat(status) : undefined;
  // a page change loads through the watcher below
  if (pagination.skip) pagination.setPage(1);
  else load();
}

load();
watch(searchValue, () => pagination.setPage(1));
watch([sortExpression, searchValue, () => pagination.skip], debounce(load, 300));
watch(() => props.reloadKey, () => load());
</script>
