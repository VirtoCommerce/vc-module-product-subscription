using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Events;

public class RenewalChangingEvent(IEnumerable<GenericChangedEntry<Renewal>> changedEntries)
    : GenericChangedEntryEvent<Renewal>(changedEntries);
