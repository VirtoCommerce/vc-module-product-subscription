<template>
  <VcBlade
    :title="$t('PRODUCT_SUBSCRIPTION.PAGES.LIST.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading || running"
    width="50%"
  >
    <VcContainer v-if="!canRead">
      <VcBanner
        variant="warning"
        icon="lucide-lock"
      >
        {{ $t("PRODUCT_SUBSCRIPTION.ALERTS.NO_READ_PERMISSION") }}
      </VcBanner>
    </VcContainer>
    <SubscriptionsTable
      v-else
      :active-item-id="activeChildParam"
      :reload-key="reloadKey"
      @open="(s) => openBlade({ name: 'ProductSubscriptionDetails', param: s.id })"
      @loading="(v) => (loading = v)"
    />
  </VcBlade>
</template>

<script setup lang="ts">
import { notification, useBlade, usePermissions, type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcContainer } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import SubscriptionsTable from "../components/SubscriptionsTable.vue";
import { runScheduler, useAction } from "../api";

defineBlade({
  url: "/product-subscriptions",
  name: "ProductSubscriptionsList",
  isWorkspace: true,
  permissions: ["product-subscription:access"],
  menuItem: {
    title: "PRODUCT_SUBSCRIPTION.MENU.TITLE",
    icon: "lucide-repeat",
    priority: 950,
  },
});

const { t } = useI18n({ useScope: "global" });
const { activeChildParam, openBlade, exposeToChildren } = useBlade();
// The menu needs "access"; the list itself needs "read" (the REST search answers 403 without it).
const canRead = usePermissions().hasAccess("product-subscription:read");

const loading = ref(false);
const reloadKey = ref(0);
const reload = () => reloadKey.value++;

const { loading: running, action: runNow } = useAction(async () => {
  const result = await runScheduler();
  const count = result?.processed ?? 0;
  notification.success(t("PRODUCT_SUBSCRIPTION.ALERTS.SCHEDULER_DONE", { count }, count), { timeout: 5000 });
  reload();
});

const bladeToolbar = ref<IBladeToolbar[]>([
  { id: "refresh", title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.REFRESH")), icon: "lucide-refresh-cw", clickHandler: reload },
  {
    id: "renewals",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.RENEWALS")),
    icon: "lucide-truck",
    permissions: "product-subscription:read",
    clickHandler: () => openBlade({ name: "ProductSubscriptionRenewals" }),
  },
  {
    id: "runScheduler",
    title: computed(() => t("PRODUCT_SUBSCRIPTION.TOOLBAR.RUN_SCHEDULER")),
    icon: "lucide-play",
    permissions: "product-subscription:renewals:process",
    clickHandler: () => runNow(),
  },
]);

exposeToChildren({ reload });
</script>
