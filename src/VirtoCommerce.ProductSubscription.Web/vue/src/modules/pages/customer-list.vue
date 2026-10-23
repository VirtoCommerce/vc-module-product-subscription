<template>
  <VcBlade
    :title="$t('PRODUCT_SUBSCRIPTION.PAGES.LIST.CUSTOMER_TITLE')"
    :subtitle="options?.customerName"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <SubscriptionsTable
      :customer-id="param"
      :active-item-id="activeChildParam"
      :reload-key="reloadKey"
      @open="(s) => openBlade({ name: 'ProductSubscriptionDetails', param: s.id })"
      @loading="(v) => (loading = v)"
    />
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, type IBladeToolbar } from "@vc-shell/framework";
import { VcBlade } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import SubscriptionsTable from "../components/SubscriptionsTable.vue";

// The subscriptions of one customer (param = the customer's user or contact id), from the contact's widget
defineBlade({
  url: "/customer-product-subscriptions",
  name: "ProductSubscriptionsCustomerList",
  permissions: ["product-subscription:read"],
});

const { t } = useI18n({ useScope: "global" });
const { param, options, activeChildParam, openBlade, exposeToChildren } = useBlade<{ customerName?: string }>();

const loading = ref(false);
const reloadKey = ref(0);
const reload = () => reloadKey.value++;

const bladeToolbar = ref<IBladeToolbar[]>([
  { id: "refresh", title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.REFRESH")), icon: "lucide-refresh-cw", clickHandler: reload },
]);

exposeToChildren({ reload });
</script>
