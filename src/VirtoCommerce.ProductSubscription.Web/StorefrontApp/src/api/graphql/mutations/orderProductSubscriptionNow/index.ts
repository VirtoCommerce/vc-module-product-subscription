import { graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { OrderProductSubscriptionNowDocument } from "../../types";
import type { InputProductSubscriptionId, OrderProductSubscriptionNowMutation } from "../../types";

export async function orderProductSubscriptionNow(
  command: InputProductSubscriptionId,
): Promise<OrderProductSubscriptionNowMutation["orderProductSubscriptionNow"]> {
  const { data } = await graphqlClient.mutate({
    mutation: OrderProductSubscriptionNowDocument,
    context: SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT,
    variables: { command },
  });

  return data!.orderProductSubscriptionNow;
}
