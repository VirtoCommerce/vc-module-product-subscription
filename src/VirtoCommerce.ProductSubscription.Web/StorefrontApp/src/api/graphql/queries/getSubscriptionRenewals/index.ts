import { globals, graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { GetSubscriptionRenewalsDocument } from "../../types";
import type { GetSubscriptionRenewalsQuery, GetSubscriptionRenewalsQueryVariables } from "../../types";

export async function getSubscriptionRenewals(
  variables: Omit<GetSubscriptionRenewalsQueryVariables, "storeId">,
  options: { suppressErrorNotifications?: boolean } = {},
): Promise<GetSubscriptionRenewalsQuery["subscriptionRenewals"]> {
  const { data } = await graphqlClient.query({
    query: GetSubscriptionRenewalsDocument,
    variables: { storeId: globals.storeId ?? "", cultureName: globals.cultureName, ...variables },
    fetchPolicy: "network-only",
    // A page that shows its own load error asks for no global toast on top of it.
    context: options.suppressErrorNotifications ? SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT : undefined,
  });

  return data.subscriptionRenewals;
}
