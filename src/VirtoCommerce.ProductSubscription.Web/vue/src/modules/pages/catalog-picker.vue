<template>
  <!-- Replaces the Catalog module's AngularJS "select items" blade: keyword search over every catalog -->
  <VcBlade
    :title="$t('PRODUCT_SUBSCRIPTION.PAGES.PICKER.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcDataTable
      v-model:search-value="searchValue"
      v-model:selection="selection"
      selection-mode="multiple"
      :items="items"
      :total-count="totalCount"
      :pagination="pagination"
      :searchable="true"
      :search-placeholder="$t('PRODUCT_SUBSCRIPTION.PAGES.PICKER.SEARCH')"
      state-key="PRODUCT_SUBSCRIPTION_CATALOG_PICKER"
      :global-filters="globalFilters"
      :empty-state="{ icon: 'lucide-package-search', title: $t('PRODUCT_SUBSCRIPTION.PAGES.PICKER.EMPTY') }"
      @row-click="(e: { data: CatalogEntry }) => toggle(e.data)"
      @pagination-click="pagination.goToPage"
      @filter="onFilter"
    >
      <VcColumn
        id="imageUrl"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.IMAGE')"
        type="image"
        width="72px"
        mobile-role="image"
      />
      <VcColumn
        id="name"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.NAME')"
        always-visible
        mobile-role="title"
      />
      <VcColumn
        id="type"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.TYPE')"
        width="140px"
        always-visible
        mobile-role="status"
      >
        <template #body="{ data: e }">
          <span class="ps-type">
            <VcIcon
              :icon="e.type === 'category' ? 'lucide-folder' : 'lucide-package'"
              size="s"
            />
            {{ e.type === "category" ? $t("PRODUCT_SUBSCRIPTION.FIELDS.CATEGORY") : $t("PRODUCT_SUBSCRIPTION.FIELDS.PRODUCT") }}
          </span>
        </template>
      </VcColumn>
      <VcColumn
        id="code"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.CODE')"
        width="160px"
        mobile-role="field"
      />
      <!-- the catalog and parent categories: names repeat across catalogs -->
      <VcColumn
        id="path"
        :title="$t('PRODUCT_SUBSCRIPTION.FIELDS.LOCATION')"
        mobile-role="field"
      >
        <template #body="{ data: e }">{{ (e.path ?? []).slice(0, -1).join(" › ") || "—" }}</template>
      </VcColumn>
    </VcDataTable>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, useDataTablePagination, useFunctions, useLatestRequest, useTableSearch, type IBladeToolbar } from "@vc-shell/framework";
import { VcBlade, VcColumn, VcDataTable, VcIcon } from "@vc-shell/framework/ui";
import { computed, ref, watch } from "vue";
import { searchCatalog, useFormat, type CatalogEntry, useAction } from "../api";

// no url: the picker only works over the plan that opened it
defineBlade({
  name: "ProductSubscriptionCatalogPicker",
  permissions: ["product-subscription:plans:manage"],
});

const PAGE_SIZE = 20;
const ALL_TYPES = ["Category", "CatalogProduct"];
const f = useFormat();
const { callParent, closeSelf } = useBlade();
const { debounce } = useFunctions();

const items = ref<CatalogEntry[]>([]);
const totalCount = ref(0);
const selection = ref<CatalogEntry[]>([]);
const types = ref(ALL_TYPES);
// no stateKey: kept in the URL, they would replace the route of the plan under this blade
const { searchValue } = useTableSearch();
const pagination = useDataTablePagination({ pageSize: PAGE_SIZE, totalCount });
const search = useLatestRequest();

const { loading, action: load } = useAction(async () => {
  // nothing until a keyword: listing every catalog's entries is slow and holds up the search that follows
  const keyword = searchValue.value?.trim();
  if (!keyword) {
    items.value = [];
    totalCount.value = 0;
    return;
  }
  const result = await search.latest(searchCatalog(keyword, types.value, pagination.skip, PAGE_SIZE));
  if (!result) return;
  items.value = result.results;
  totalCount.value = result.totalCount;
});

const globalFilters = computed(() => [
  {
    id: "type",
    label: f.t("PRODUCT_SUBSCRIPTION.FIELDS.TYPE"),
    filter: {
      options: [
        { value: "CatalogProduct", label: f.t("PRODUCT_SUBSCRIPTION.PAGES.PICKER.PRODUCTS") },
        { value: "Category", label: f.t("PRODUCT_SUBSCRIPTION.PAGES.PICKER.CATEGORIES") },
      ],
    },
  },
]);

function onFilter(event: { filters: Record<string, unknown> }) {
  const type = event.filters.type as string | string[] | undefined;
  types.value = type && type.length ? ([] as string[]).concat(type) : ALL_TYPES;
  if (pagination.skip) pagination.setPage(1);
  else load();
}

load();
watch(searchValue, () => pagination.setPage(1));
watch([searchValue, () => pagination.skip], debounce(load, 300));

// a tap selects too: on phones the cards have no checkboxes
function toggle(entry: CatalogEntry) {
  selection.value = selection.value.some((x) => x.id === entry.id)
    ? selection.value.filter((x) => x.id !== entry.id)
    : [...selection.value, entry];
}

const { loading: adding, action: add } = useAction(async () => {
  await callParent("addCatalogItems", selection.value);
  await closeSelf();
});

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "add",
    title: computed(() => f.t("PRODUCT_SUBSCRIPTION.TOOLBAR.ADD_SELECTED")),
    icon: "lucide-plus",
    disabled: computed(() => !selection.value.length || adding.value),
    clickHandler: () => add(),
  },
]);
</script>
