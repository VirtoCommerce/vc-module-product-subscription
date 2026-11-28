using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Models;
using VirtoCommerce.ProductSubscription.Data.Repositories;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Services;

/// <summary>The filters the scheduler relies on, run against the search service's query.</summary>
[Trait("Category", "Unit")]
public class SubscriptionSearchQueryTests
{
    private static readonly DateTime _now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<SubscriptionEntity> _subscriptions = [];
    private readonly List<RenewalItemEntity> _renewalItems = [];
    private readonly Mock<IProductSubscriptionRepository> _repository = new();
    private readonly QueryableSubscriptionSearchService _searchService;

    public SubscriptionSearchQueryTests()
    {
        _repository.Setup(x => x.Subscriptions).Returns(() => _subscriptions.AsQueryable());
        _repository.Setup(x => x.RenewalItems).Returns(() => _renewalItems.AsQueryable());

        _searchService = new QueryableSubscriptionSearchService(
            () => _repository.Object,
            Mock.Of<IPlatformMemoryCache>(),
            Mock.Of<ISubscriptionService>(),
            Options.Create(new CrudOptions()));
    }

    [Fact]
    public void WithoutOpenRenewal_IgnoresLinksToRenewalsThatEndedOrDroppedTheSubscription()
    {
        var free = AddSubscription("free");
        var inOpenRenewal = AddSubscription("in-open", "renewal-open");
        var linkedToEnded = AddSubscription("linked-to-ended", "renewal-cancelled");
        var droppedFromOpen = AddSubscription("dropped-from-open", "renewal-open-2");
        AddRenewalItem("renewal-open", RenewalState.Notified, inOpenRenewal.Id);
        AddRenewalItem("renewal-cancelled", RenewalState.Cancelled, linkedToEnded.Id);
        AddRenewalItem("renewal-open-2", RenewalState.Scheduled, "another-subscription");

        var ids = Search(new SubscriptionSearchCriteria { WithoutOpenRenewal = true });

        ids.Should().BeEquivalentTo([free.Id, linkedToEnded.Id, droppedFromOpen.Id]);
    }

    [Fact]
    public void CurrentRenewalId_FindsTheSubscriptionsLinkedToARenewal()
    {
        var linked = AddSubscription("linked", "renewal-1");
        AddSubscription("other", "renewal-2");

        Search(new SubscriptionSearchCriteria { CurrentRenewalId = "renewal-1" }).Should().Equal(linked.Id);
    }

    [Fact]
    public void PausedUntilBefore_FindsPausesThatEnded()
    {
        var ended = AddSubscription("ended", configure: x => x.PausedUntil = _now.AddHours(-1));
        AddSubscription("later", configure: x => x.PausedUntil = _now.AddDays(1));
        AddSubscription("open-ended");

        Search(new SubscriptionSearchCriteria { PausedUntilBefore = _now }).Should().Equal(ended.Id);
    }

    [Fact]
    public void AnnualReminderDueBefore_UsesTheStartDateBeforeTheFirstReminder()
    {
        var neverReminded = AddSubscription("never-reminded", configure: x => x.StartDate = _now.AddYears(-2));
        var remindedLongAgo = AddSubscription("reminded-long-ago", configure: x => x.LastAnnualReminderDate = _now.AddMonths(-13));
        AddSubscription("reminded-recently", configure: x => x.LastAnnualReminderDate = _now.AddMonths(-1));
        AddSubscription("new", configure: x => x.StartDate = _now.AddMonths(-2));

        var ids = Search(new SubscriptionSearchCriteria { AnnualReminderDueBefore = _now.AddYears(-1) });

        ids.Should().BeEquivalentTo([neverReminded.Id, remindedLongAgo.Id]);
    }

    private IList<string> Search(SubscriptionSearchCriteria criteria)
    {
        return _searchService.Query(_repository.Object, criteria).Select(x => x.Id).ToList();
    }

    private SubscriptionEntity AddSubscription(string id, string currentRenewalId = null, Action<SubscriptionEntity> configure = null)
    {
        var entity = new SubscriptionEntity
        {
            Id = id,
            Status = SubscriptionStatus.Active,
            StartDate = _now.AddMonths(-1),
            CurrentRenewalId = currentRenewalId,
        };

        configure?.Invoke(entity);
        _subscriptions.Add(entity);
        return entity;
    }

    private void AddRenewalItem(string renewalId, string state, string subscriptionId)
    {
        _renewalItems.Add(new RenewalItemEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            RenewalId = renewalId,
            Renewal = new RenewalEntity { Id = renewalId, State = state },
            SubscriptionId = subscriptionId,
        });
    }
}
