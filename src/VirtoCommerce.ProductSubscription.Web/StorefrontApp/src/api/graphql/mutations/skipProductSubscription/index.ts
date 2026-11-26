import { graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { SkipProductSubscriptionDocument } from "../../types";
import type { InputSkipProductSubscription, SkipProductSubscriptionMutation } from "../../types";

export async function skipProductSubscription(
  command: InputSkipProductSubscription,
): Promise<SkipProductSubscriptionMutation["skipProductSubscription"]> {
  const { data } = await graphqlClient.mutate({
    mutation: SkipProductSubscriptionDocument,
    context: SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT,
    variables: { command },
  });

  return data!.skipProductSubscription;
}
