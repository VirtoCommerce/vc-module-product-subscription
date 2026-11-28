import { graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { RemoveCartItemSubscriptionDocument } from "../../types";
import type { InputRemoveCartItemSubscription, RemoveCartItemSubscriptionMutation } from "../../types";

export async function removeCartItemSubscription(
  command: InputRemoveCartItemSubscription,
): Promise<RemoveCartItemSubscriptionMutation["removeCartItemSubscription"]> {
  const { data } = await graphqlClient.mutate({
    mutation: RemoveCartItemSubscriptionDocument,
    context: SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT,
    variables: { command },
  });

  return data!.removeCartItemSubscription;
}
