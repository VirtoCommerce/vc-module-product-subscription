<template>
  <VcBlade
    :title="$t('PRODUCT_SUBSCRIPTION.PAGES.RENEWALS.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcDataTable
      v-model:search-value="searchValue"
      v-model:sort-field="sortField"
      v-model:sort-order="sortOrder"
      :items="items"
      :total-count="totalCount"
      :pagination="pagination"
      :searchable="true"
      :search-placeholder="$t('PRODUCT_SUBSCRIPTION.PAGES.RENEWALS.SEARCH')"
      state-key="PRODUCT_SUBSCRIPTION_RENEWALS"
      :active-item-id="activeChildParam"
      :show-all-columns="!activeChildParam"
      :global-filters="globalFilters"
      :empty-state="{ icon: 'lucide-truck', title: $t('PRODUCT_SUBSCRIPTION.PAGES.RENEWALS.EMPTY') }"
      @row-click="(e: { data: Renewal }) => openBlade({ name: 'ProductSubscriptionRenewalDetails', param: e.data.id })"
      @pagination-click="pagination.goToPage"
      @filter="onFilter"
    >
      <VcColumn
        id="number"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.NUMBER')"
        width="150px"
        sortable
        always-visible
        mobile-role="title"
      />
      <VcColumn
        id="state"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.STATE')"
        width="190px"
        sortable
        always-visible
        mobile-role="status"
      >
        <template #body="{ data: r }">
          <div class="ps-inline">
            <VcStatus :variant="statusVariant(r.state)">{{ f.status(r.state) }}</VcStatus>
          </div>
        </template>
      </VcColumn>
      <VcColumn
        id="customerName"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.CUSTOMER')"
        sortable
        mobile-role="field"
      />
      <VcColumn
        id="scheduledDate"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.DELIVERY_DATE')"
        width="130px"
        sortable
        always-visible
        mobile-role="field"
      >
        <template #body="{ data: r }">{{ f.date(r.scheduledDate) }}</template>
      </VcColumn>
      <VcColumn
        id="orderNumber"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.ORDER')"
        width="140px"
        sortable
        mobile-role="field"
      >
        <template #body="{ data: r }">{{ r.orderNumber || "—" }}</template>
      </VcColumn>
      <VcColumn
        id="previewTotal"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.TOTAL')"
        width="120px"
        align="end"
        sortable
        mobile-role="field"
      >
        <template #body="{ data: r }">
          <span class="vc-table-cell-money">{{ f.money(r.orderTotal ?? r.previewTotal, r.currency) }}</span>
        </template>
      </VcColumn>
    </VcDataTable>
  </VcBlade>
</template>

<script setup lang="ts">
import {
  useBlade,
  useDataTablePagination,
  useDataTableSort,
  useFunctions,
  useLatestRequest,
  useTableSearch,
  type IBladeToolbar,
} from "@vc-shell/framework";
import { VcBlade, VcColumn, VcDataTable, VcStatus } from "@vc-shell/framework/ui";
import { computed, ref, watch } from "vue";
import type { Renewal } from "../../api_client/virtocommerce.productsubscription";
import { renewalStates, statusVariant, useFormat, useProductSubscriptionClient, useAction } from "../api";

defineBlade({
  url: "/product-subscription-renewals",
  name: "ProductSubscriptionRenewals",
  permissions: ["product-subscription:read"],
});

const STATE_KEY = "PRODUCT_SUBSCRIPTION_RENEWALS";
const PAGE_SIZE = 20;
const f = useFormat();
const { activeChildParam, openBlade, exposeToChildren } = useBlade();
const { debounce } = useFunctions();
const { getApiClient } = useProductSubscriptionClient();

const items = ref<Renewal[]>([]);
const totalCount = ref(0);
const filters = ref<{ states?: string[]; onlyOpen?: boolean }>({});

const { sortField, sortOrder, sortExpression } = useDataTableSort({ stateKey: STATE_KEY, initialField: "scheduledDate", initialDirection: "DESC" });
const { searchValue } = useTableSearch({ stateKey: STATE_KEY });
const pagination = useDataTablePagination({ stateKey: STATE_KEY, pageSize: PAGE_SIZE, totalCount });
const search = useLatestRequest();

const { loading, action: load } = useAction(async () => {
  const result = await search.latest(
    (await getApiClient()).searchRenewals({
      keyword: searchValue.value || undefined,
      ...filters.value,
      sort: sortExpression.value,
      skip: pagination.skip,
      take: PAGE_SIZE,
    }),
  );
  if (!result) return;
  items.value = result.results ?? [];
  totalCount.value = result.totalCount ?? 0;
});

const globalFilters = computed(() => [
  {
    id: "state",
    label: f.t("PRODUCT_SUBSCRIPTION.FIELDS.STATE"),
    filter: { options: renewalStates.map((s) => ({ value: s, label: f.status(s) })) },
  },
  {
    id: "open",
    label: f.t("PRODUCT_SUBSCRIPTION.FIELDS.ONLY_OPEN"),
    filter: { options: [{ value: "open", label: f.t("PRODUCT_SUBSCRIPTION.FIELDS.ONLY_OPEN") }] },
  },
]);

function onFilter(event: { filters: Record<string, unknown> }) {
  const state = event.filters.state as string | string[] | undefined;
  const open = event.filters.open as string | string[] | undefined;
  filters.value = {
    states: state && state.length ? ([] as string[]).concat(state) : undefined,
    onlyOpen: open && open.length ? true : undefined,
  };
  if (pagination.skip) pagination.setPage(1);
  else load();
}

load();
watch(searchValue, () => pagination.setPage(1));
watch([sortExpression, searchValue, () => pagination.skip], debounce(load, 300));

const bladeToolbar = ref<IBladeToolbar[]>([
  { id: "refresh", title: computed(() => f.t("PRODUCT_SUBSCRIPTION.TOOLBAR.REFRESH")), icon: "lucide-refresh-cw", clickHandler: () => load() },
]);

exposeToChildren({ reload: () => load() });
</script>
