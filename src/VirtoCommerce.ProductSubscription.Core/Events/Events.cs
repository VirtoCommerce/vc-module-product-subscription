using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Events;

public class SubscriptionPlanChangingEvent(IEnumerable<GenericChangedEntry<SubscriptionPlan>> changedEntries)
    : GenericChangedEntryEvent<SubscriptionPlan>(changedEntries);

public class SubscriptionPlanChangedEvent(IEnumerable<GenericChangedEntry<SubscriptionPlan>> changedEntries)
    : GenericChangedEntryEvent<SubscriptionPlan>(changedEntries);

public class SubscriptionChangingEvent(IEnumerable<GenericChangedEntry<Subscription>> changedEntries)
    : GenericChangedEntryEvent<Subscription>(changedEntries);

/// <summary>Published for every subscription change; picked up by the Event Bus and Webhooks modules.</summary>
public class SubscriptionChangedEvent(IEnumerable<GenericChangedEntry<Subscription>> changedEntries)
    : GenericChangedEntryEvent<Subscription>(changedEntries);

public class RenewalChangingEvent(IEnumerable<GenericChangedEntry<Renewal>> changedEntries)
    : GenericChangedEntryEvent<Renewal>(changedEntries);

/// <summary>Published for every renewal change (state transitions included); picked up by the Event Bus and Webhooks modules.</summary>
public class RenewalChangedEvent(IEnumerable<GenericChangedEntry<Renewal>> changedEntries)
    : GenericChangedEntryEvent<Renewal>(changedEntries);

public class SubscriptionCartItemChangingEvent(IEnumerable<GenericChangedEntry<SubscriptionCartItem>> changedEntries)
    : GenericChangedEntryEvent<SubscriptionCartItem>(changedEntries);

public class SubscriptionCartItemChangedEvent(IEnumerable<GenericChangedEntry<SubscriptionCartItem>> changedEntries)
    : GenericChangedEntryEvent<SubscriptionCartItem>(changedEntries);
