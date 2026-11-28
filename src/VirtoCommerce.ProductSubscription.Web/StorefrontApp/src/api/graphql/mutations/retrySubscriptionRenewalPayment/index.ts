import { graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { RetrySubscriptionRenewalPaymentDocument } from "../../types";
import type { InputSubscriptionRenewalId, RetrySubscriptionRenewalPaymentMutation } from "../../types";

export async function retrySubscriptionRenewalPayment(
  command: InputSubscriptionRenewalId,
): Promise<RetrySubscriptionRenewalPaymentMutation["retrySubscriptionRenewalPayment"]> {
  const { data } = await graphqlClient.mutate({
    mutation: RetrySubscriptionRenewalPaymentDocument,
    context: SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT,
    variables: { command },
  });

  return data!.retrySubscriptionRenewalPayment;
}
