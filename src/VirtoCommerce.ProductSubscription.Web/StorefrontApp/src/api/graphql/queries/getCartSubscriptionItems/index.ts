import { graphqlClient, SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT } from "@vc-frontend/core";
import { GetCartSubscriptionItemsDocument } from "../../types";
import type { GetCartSubscriptionItemsQuery } from "../../types";

export async function getCartSubscriptionItems(
  cartId: string,
): Promise<GetCartSubscriptionItemsQuery["cartSubscriptionItems"]> {
  const { data } = await graphqlClient.query({
    query: GetCartSubscriptionItemsDocument,
    context: SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT,
    variables: { cartId },
    fetchPolicy: "network-only",
  });

  return data.cartSubscriptionItems;
}
