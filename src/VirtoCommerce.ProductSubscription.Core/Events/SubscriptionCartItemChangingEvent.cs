using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Events;

public class SubscriptionCartItemChangingEvent(IEnumerable<GenericChangedEntry<SubscriptionCartItem>> changedEntries)
    : GenericChangedEntryEvent<SubscriptionCartItem>(changedEntries);
