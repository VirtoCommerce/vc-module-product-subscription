import { globals, graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { GetSubscriptionTermsDocument } from "../../types";
import type { GetSubscriptionTermsQuery } from "../../types";

export async function getSubscriptionTerms(): Promise<GetSubscriptionTermsQuery["subscriptionTerms"]> {
  const { data } = await graphqlClient.query({
    query: GetSubscriptionTermsDocument,
    context: SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT,
    variables: { storeId: globals.storeId ?? "", cultureName: globals.cultureName },
    fetchPolicy: "network-only",
  });

  return data.subscriptionTerms;
}
