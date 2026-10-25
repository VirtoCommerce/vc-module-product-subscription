using System;
using System.Linq;
using FluentAssertions;
using Moq;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Repositories;
using VirtoCommerce.ProductSubscription.Data.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Services;

/// <summary>The plan service validates every plan it saves, whoever saves it (it used to be the REST controller only).</summary>
[Trait("Category", "Unit")]
public class SubscriptionPlanValidationTests
{
    private readonly ValidatingPlanService _service = new();

    [Theory]
    [InlineData("", "store-1", "Enter a plan name.")]
    [InlineData("Monthly", "", "Choose a store.")]
    public void PlanWithoutNameOrStore_IsRejected(string name, string storeId, string message)
    {
        var plan = ValidPlan();
        plan.Name = name;
        plan.StoreId = storeId;

        var validate = () => _service.Check(plan);

        validate.Should().Throw<SubscriptionOperationException>().WithMessage(message);
    }

    [Fact]
    public void PlanWithoutFrequencies_IsRejected()
    {
        var plan = ValidPlan();
        plan.Intervals = [];

        var validate = () => _service.Check(plan);

        validate.Should().Throw<SubscriptionOperationException>().WithMessage("Add at least one delivery frequency.");
    }

    [Fact]
    public void PercentDiscountOver100_IsRejected()
    {
        var plan = ValidPlan();
        plan.DiscountType = SubscriptionDiscountType.Percent;
        plan.DiscountValue = 120;

        var validate = () => _service.Check(plan);

        validate.Should().Throw<SubscriptionOperationException>();
    }

    [Fact]
    public void PlanWithoutDefaultFrequency_GetsTheFirstOneAsDefault()
    {
        var plan = ValidPlan();
        plan.Intervals.Add(new SubscriptionPlanInterval { Unit = SubscriptionIntervalUnit.Week, Count = 2 });

        _service.Check(plan);

        plan.Intervals.Select(x => x.IsDefault).Should().Equal(true, false);
    }

    private static SubscriptionPlan ValidPlan() => new()
    {
        Name = "Monthly",
        StoreId = "store-1",
        Intervals = [new SubscriptionPlanInterval { Unit = SubscriptionIntervalUnit.Month, Count = 1 }],
    };

    private sealed class ValidatingPlanService()
        : SubscriptionPlanService(Mock.Of<Func<IProductSubscriptionRepository>>(), Mock.Of<IPlatformMemoryCache>(), Mock.Of<IEventPublisher>())
    {
        public void Check(SubscriptionPlan plan) => Validate(plan);
    }
}
