using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Events;

/// <summary>Published for every subscription change; picked up by the Event Bus and Webhooks modules.</summary>
public class SubscriptionChangedEvent(IEnumerable<GenericChangedEntry<Subscription>> changedEntries)
    : GenericChangedEntryEvent<Subscription>(changedEntries);
