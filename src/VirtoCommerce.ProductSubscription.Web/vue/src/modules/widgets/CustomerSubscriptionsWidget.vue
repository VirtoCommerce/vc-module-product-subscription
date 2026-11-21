<template>
  <VcWidget
    icon="lucide-repeat"
    :title="$t('PRODUCT_SUBSCRIPTION.WIDGETS.SUBSCRIPTIONS')"
    :value="count"
    :disabled="!customerId"
    @click="open"
  />
</template>

<script setup lang="ts">
import { injectBladeContext, useBlade, useWidgetTrigger } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";
import { computed, ref, watch } from "vue";
import { useProductSubscriptionClient } from "../api";

// The subscriptions of a contact, on the Customer module's contact blade (the AngularJS widget was on customerDetail1).
interface Contact {
  id?: string;
  fullName?: string;
  name?: string;
  securityAccounts?: { id?: string }[];
}

const ctx = injectBladeContext();
const contact = computed(() => ctx.value.item as Contact | undefined);
const { openBlade } = useBlade();
const { getApiClient } = useProductSubscriptionClient();

// subscriptions belong to the contact's first user account, as orders placed in the store do; the contact id otherwise
const customerId = computed(() => contact.value?.securityAccounts?.find((a) => a.id)?.id ?? contact.value?.id);
const count = ref<number | string>("…");

async function refresh() {
  if (!customerId.value) {
    count.value = 0;
    return;
  }
  const result = await (await getApiClient()).searchSubscriptions({ customerId: customerId.value, take: 0 }).catch(() => undefined);
  count.value = result?.totalCount ?? "—";
}

watch(customerId, refresh, { immediate: true });
useWidgetTrigger({ onRefresh: refresh });

const open = () =>
  openBlade({
    name: "ProductSubscriptionsCustomerList",
    param: customerId.value,
    options: { customerName: contact.value?.fullName ?? contact.value?.name },
  });
</script>
