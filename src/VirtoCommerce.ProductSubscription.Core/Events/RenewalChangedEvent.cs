using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Events;

/// <summary>Published for every renewal change (state transitions included); picked up by the Event Bus and Webhooks modules.</summary>
public class RenewalChangedEvent(IEnumerable<GenericChangedEntry<Renewal>> changedEntries)
    : GenericChangedEntryEvent<Renewal>(changedEntries);
