<template>
  <VcBlade
    :title="isNew ? $t('PRODUCT_SUBSCRIPTION.PAGES.PLAN.NEW_TITLE') : (loadedName ?? '')"
    :subtitle="$t('PRODUCT_SUBSCRIPTION.PAGES.PLAN.SUBTITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    :modified="form.isModified.value"
    width="50%"
  >
    <VcContainer>
      <div
        v-if="ready"
        class="ps-page"
      >
        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.PAGES.PLAN.GENERAL')"
          icon="lucide-tag"
        >
          <div class="ps-card">
            <VcInput
              v-model="item.name"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.NAME')"
              :placeholder="$t('PRODUCT_SUBSCRIPTION.PAGES.PLAN.NAME_PLACEHOLDER')"
              maxlength="256"
              :disabled="readonly"
              required
            />
            <VcTextarea
              v-model="item.description"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.DESCRIPTION')"
              maxlength="2048"
              :disabled="readonly"
            />
            <div class="ps-grid">
              <VcSelect
                v-model="item.storeId"
                :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.STORE')"
                :options="searchStores"
                option-value="id"
                option-label="name"
                :clearable="false"
                :disabled="readonly"
                searchable
                required
              />
              <VcInput
                :model-value="item.priority ?? 0"
                type="integer"
                :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.PRIORITY')"
                :disabled="readonly"
                @update:model-value="(v) => (item.priority = Number(v) || 0)"
              />
            </div>
            <VcSwitch
              v-model="item.isActive"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.ACTIVE')"
              :disabled="readonly"
            />
          </div>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.FIELDS.DISCOUNT')"
          icon="lucide-percent"
        >
          <div class="ps-card ps-grid">
            <VcSelect
              v-model="item.discountType"
              :label="$t('PRODUCT_SUBSCRIPTION.FIELDS.DISCOUNT_TYPE')"
              :options="discountTypes"
              :clearable="false"
              :disabled="readonly"
            />
            <VcInput
              :model-value="item.discountValue ?? 0"
              type="number"
              step="0.01"
              :label="
                item.discountType === 'Percent'
                  ? $t('PRODUCT_SUBSCRIPTION.FIELDS.DISCOUNT_PERCENT')
                  : $t('PRODUCT_SUBSCRIPTION.FIELDS.DISCOUNT_AMOUNT')
              "
              :disabled="readonly || item.discountType === 'None'"
              @update:model-value="(v) => (item.discountValue = Math.max(0, Number(v) || 0))"
            />
          </div>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.FIELDS.FREQUENCIES')"
          icon="lucide-calendar-sync"
        >
          <div class="ps-card">
            <VcHint v-if="!item.intervals?.length">{{ $t("PRODUCT_SUBSCRIPTION.PAGES.PLAN.NO_FREQUENCIES") }}</VcHint>
            <div
              v-for="(interval, index) in item.intervals"
              :key="index"
              class="ps-interval"
            >
              <VcInput
                :model-value="interval.count ?? 1"
                type="integer"
                class="ps-interval__count"
                :label="index === 0 ? $t('PRODUCT_SUBSCRIPTION.FIELDS.EVERY') : undefined"
                :aria-label="$t('PRODUCT_SUBSCRIPTION.FIELDS.EVERY')"
                :disabled="readonly"
                @update:model-value="(v) => (interval.count = Math.min(365, Math.max(1, Number(v) || 1)))"
              />
              <VcSelect
                v-model="interval.unit"
                class="ps-interval__unit"
                :label="index === 0 ? $t('PRODUCT_SUBSCRIPTION.FIELDS.UNIT') : undefined"
                :options="units"
                :clearable="false"
                :disabled="readonly"
              />
              <label class="ps-interval__default">
                <input
                  type="radio"
                  name="ps-default-interval"
                  :checked="interval.isDefault"
                  :disabled="readonly"
                  @change="setDefault(index)"
                />
                {{ $t("PRODUCT_SUBSCRIPTION.FIELDS.DEFAULT") }}
              </label>
              <VcButton
                variant="ghost"
                icon="lucide-trash-2"
                :aria-label="$t('PRODUCT_SUBSCRIPTION.PAGES.PLAN.REMOVE_FREQUENCY')"
                :disabled="readonly"
                @click="removeInterval(index)"
              />
            </div>
            <div>
              <VcButton
                variant="secondary"
                icon="lucide-plus"
                :disabled="readonly"
                @click="addInterval"
              >
                {{ $t("PRODUCT_SUBSCRIPTION.PAGES.PLAN.ADD_FREQUENCY") }}
              </VcButton>
            </div>
          </div>
        </VcCard>

        <VcCard
          :header="$t('PRODUCT_SUBSCRIPTION.FIELDS.APPLIES_TO')"
          icon="lucide-boxes"
        >
          <div class="ps-card">
            <VcHint>{{ $t("PRODUCT_SUBSCRIPTION.PAGES.PLAN.APPLIES_HINT") }}</VcHint>
            <div
              v-if="item.categoryIds?.length || item.productIds?.length"
              class="ps-chips"
            >
              <span
                v-for="id in item.categoryIds"
                :key="id"
                class="ps-chip"
              >
                <VcIcon
                  icon="lucide-folder"
                  size="s"
                />
                <span class="ps-chip__text">{{ names[id] ?? id }}</span>
                <button
                  v-if="!readonly"
                  type="button"
                  class="ps-chip__remove"
                  :aria-label="$t('PRODUCT_SUBSCRIPTION.PAGES.PLAN.REMOVE_ITEM')"
                  @click="item.categoryIds = item.categoryIds!.filter((x) => x !== id)"
                >
                  <VcIcon
                    icon="lucide-x"
                    size="xs"
                  />
                </button>
              </span>
              <span
                v-for="id in item.productIds"
                :key="id"
                class="ps-chip"
              >
                <VcIcon
                  icon="lucide-package"
                  size="s"
                />
                <span class="ps-chip__text">{{ names[id] ?? id }}</span>
                <button
                  v-if="!readonly"
                  type="button"
                  class="ps-chip__remove"
                  :aria-label="$t('PRODUCT_SUBSCRIPTION.PAGES.PLAN.REMOVE_ITEM')"
                  @click="item.productIds = item.productIds!.filter((x) => x !== id)"
                >
                  <VcIcon
                    icon="lucide-x"
                    size="xs"
                  />
                </button>
              </span>
            </div>
            <div
              v-else
              class="ps-muted"
            >
              {{ $t("PRODUCT_SUBSCRIPTION.PAGES.PLAN.NOTHING_ASSIGNED") }}
            </div>
            <div>
              <VcButton
                variant="secondary"
                icon="lucide-plus"
                :disabled="readonly"
                @click="openBlade({ name: 'ProductSubscriptionCatalogPicker' })"
              >
                {{ $t("PRODUCT_SUBSCRIPTION.PAGES.PLAN.ADD_ITEMS") }}
              </VcButton>
            </div>
          </div>
        </VcCard>
      </div>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, useBladeForm, useLoading, usePermissions, usePopup, type IBladeToolbar } from "@vc-shell/framework";
import {
  VcBlade,
  VcButton,
  VcCard,
  VcContainer,
  VcHint,
  VcIcon,
  VcInput,
  VcSelect,
  VcSwitch,
  VcTextarea,
} from "@vc-shell/framework/ui";
import { computed, onMounted, ref } from "vue";
import {
  SubscriptionDiscountType,
  SubscriptionIntervalUnit,
  type SubscriptionPlan,
} from "../../api_client/virtocommerce.productsubscription";
import { catalogNames, searchStores, useFormat, useProductSubscriptionClient, withServerMessage, type CatalogEntry, useAction } from "../api";

defineBlade({
  url: "/product-subscription-plan",
  name: "ProductSubscriptionPlanDetails",
  permissions: ["product-subscription:plans:manage"],
});

const f = useFormat();
const t = f.t;
const { param, openBlade, callParent, closeSelf, replaceWith, exposeToChildren } = useBlade();
const { showConfirmation } = usePopup();
const { hasAccess } = usePermissions();
const { getApiClient } = useProductSubscriptionClient();

const item = ref<SubscriptionPlan>({});
const ready = ref(false);
const loadedName = ref<string>();
const names = ref<Record<string, string>>({});
const isNew = computed(() => !param.value);
const readonly = computed(() => !hasAccess("product-subscription:plans:manage"));

const units = computed(() => Object.values(SubscriptionIntervalUnit).map((u) => ({ id: u, title: t(`PRODUCT_SUBSCRIPTION.UNITS.${u}`) })));
const discountTypes = computed(() =>
  [SubscriptionDiscountType.Percent, SubscriptionDiscountType.FixedAmount, SubscriptionDiscountType.None].map((d) => ({
    id: d,
    title: t(`PRODUCT_SUBSCRIPTION.DISCOUNT_TYPES.${d}`),
  })),
);

const form = useBladeForm({
  data: item,
  closeConfirmMessage: computed(() => t("PRODUCT_SUBSCRIPTION.ALERTS.CLOSE_CONFIRMATION")),
});
const valid = computed(() => !!item.value.name?.trim() && !!item.value.storeId && !!item.value.intervals?.length);

function newPlan(): SubscriptionPlan {
  return {
    name: "",
    isActive: true,
    priority: 0,
    discountType: SubscriptionDiscountType.Percent,
    discountValue: 10,
    intervals: [
      { unit: SubscriptionIntervalUnit.Week, count: 2, isDefault: false },
      { unit: SubscriptionIntervalUnit.Week, count: 4, isDefault: true },
      { unit: SubscriptionIntervalUnit.Month, count: 2, isDefault: false },
    ],
    productIds: [],
    categoryIds: [],
  };
}

async function show(plan: SubscriptionPlan) {
  item.value = { ...plan, intervals: plan.intervals ?? [], productIds: plan.productIds ?? [], categoryIds: plan.categoryIds ?? [] };
  loadedName.value = plan.name;
  // the only store is the plan's store
  if (!item.value.storeId) {
    const stores = await searchStores().catch(() => ({ results: [] }));
    if (stores.results?.length === 1) item.value.storeId = stores.results[0].id;
  }
  form.setBaseline();
  ready.value = true;
  names.value = { ...names.value, ...(await catalogNames(item.value.productIds!, item.value.categoryIds!)) };
}

const { loading: fetching, action: load } = useAction(async () => {
  await show(isNew.value ? newPlan() : await (await getApiClient()).getPlan(param.value!));
});

const { loading: saving, action: save } = useAction(async () => {
  const client = await getApiClient();
  const saved = await withServerMessage(() => client.savePlan({ ...item.value, name: item.value.name?.trim() }));
  form.setBaseline();
  callParent("reload").catch(() => undefined);
  if (isNew.value) await replaceWith({ name: "ProductSubscriptionPlanDetails", param: saved.id });
  else await show(saved);
});

const loading = useLoading(fetching, saving);

function addInterval() {
  const list = (item.value.intervals ??= []);
  list.push({ unit: SubscriptionIntervalUnit.Month, count: 1, isDefault: list.length === 0 });
}

function removeInterval(index: number) {
  const list = item.value.intervals!;
  list.splice(index, 1);
  if (list.length && !list.some((x) => x.isDefault)) list[0].isDefault = true;
}

function setDefault(index: number) {
  item.value.intervals!.forEach((x, i) => (x.isDefault = i === index));
}

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "save",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.SAVE")),
    icon: "lucide-save",
    permissions: "product-subscription:plans:manage",
    disabled: computed(() => !valid.value || (!isNew.value && !form.isModified.value)),
    clickHandler: () => save(),
  },
  {
    id: "reset",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.RESET")),
    icon: "lucide-undo-2",
    disabled: computed(() => !form.isModified.value),
    clickHandler: () => form.revert(),
  },
  {
    id: "delete",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.DELETE")),
    icon: "lucide-trash-2",
    permissions: "product-subscription:plans:manage",
    disabled: computed(() => isNew.value),
    async clickHandler() {
      if (await showConfirmation(t("PRODUCT_SUBSCRIPTION.ALERTS.DELETE_PLAN"))) {
        const client = await getApiClient();
        await withServerMessage(() => client.deletePlans([param.value!]));
        form.setBaseline();
        callParent("reload").catch(() => undefined);
        await closeSelf();
      }
    },
  },
]);

onMounted(load);

exposeToChildren({
  // from the catalog picker
  addCatalogItems(entries: CatalogEntry[]) {
    const plan = item.value;
    for (const entry of entries) {
      names.value[entry.id] = entry.code ? `${entry.name} (${entry.code})` : (entry.name ?? entry.id);
      const list = entry.type === "category" ? (plan.categoryIds ??= []) : (plan.productIds ??= []);
      if (!list.includes(entry.id)) list.push(entry.id);
    }
  },
});
</script>
