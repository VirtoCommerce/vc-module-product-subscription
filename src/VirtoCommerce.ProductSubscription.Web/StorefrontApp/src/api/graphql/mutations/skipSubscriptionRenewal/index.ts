import { graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { SkipSubscriptionRenewalDocument } from "../../types";
import type { InputSubscriptionRenewalId, SkipSubscriptionRenewalMutation } from "../../types";

export async function skipSubscriptionRenewal(
  command: InputSubscriptionRenewalId,
): Promise<SkipSubscriptionRenewalMutation["skipSubscriptionRenewal"]> {
  const { data } = await graphqlClient.mutate({
    mutation: SkipSubscriptionRenewalDocument,
    context: SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT,
    variables: { command },
  });

  return data!.skipSubscriptionRenewal;
}
