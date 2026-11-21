using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using Moq;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.ProductSubscription.Xapi.Commands;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Xapi;

/// <summary>The store's "Customer actions" settings decide what a customer can do from the storefront, and the xAPI enforces them.</summary>
[Trait("Category", "Unit")]
public class CustomerActionsTests
{
    private const string StoreId = "store";
    private const string UserId = "user";
    private const string SubscriptionId = "sub-1";
    private const string RenewalId = "ren-1";
    private const string ActionDisabledCode = "SUBSCRIPTION_ACTION_DISABLED";

    private readonly Mock<ISubscriptionService> _subscriptionService = new();
    private readonly Mock<IRenewalService> _renewalService = new();
    private readonly Mock<ISubscriptionManager> _subscriptionManager = new();
    private readonly Mock<IRenewalProcessor> _renewalProcessor = new();
    private readonly Mock<ISubscriptionCustomerActionsService> _customerActions = new();
    private readonly Subscription _subscription = new()
    {
        Id = SubscriptionId,
        StoreId = StoreId,
        CustomerId = UserId,
        Status = SubscriptionStatus.Active,
        IntervalUnit = SubscriptionIntervalUnit.Week,
        IntervalCount = 1,
        StoredPaymentMethodId = "card-1",
    };

    public CustomerActionsTests()
    {
        _subscriptionService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync([_subscription]);
        _renewalService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync([new Renewal { Id = RenewalId, StoreId = StoreId, CustomerId = UserId }]);
        _subscriptionManager.Setup(x => x.SkipNextAsync(It.IsAny<string>(), It.IsAny<DateTime?>())).ReturnsAsync(_subscription);
        _subscriptionManager.Setup(x => x.PauseAsync(It.IsAny<string>(), It.IsAny<DateTime?>())).ReturnsAsync(_subscription);
        _subscriptionManager.Setup(x => x.ResumeAsync(It.IsAny<string>())).ReturnsAsync(_subscription);
        _subscriptionManager.Setup(x => x.ChangeAsync(It.IsAny<string>(), It.IsAny<SubscriptionChange>())).ReturnsAsync(_subscription);
        _renewalProcessor.Setup(x => x.SkipAsync(It.IsAny<string>())).ReturnsAsync(new Renewal());
        _renewalProcessor.Setup(x => x.OrderNowAsync(It.IsAny<string>())).ReturnsAsync(new Renewal());
    }

    private void Allow(SubscriptionCustomerActions actions)
    {
        _customerActions.Setup(x => x.GetAsync(StoreId)).ReturnsAsync(actions);
    }

    private ProductSubscriptionCommandHandler SubscriptionHandler()
        => new(_subscriptionService.Object, _subscriptionManager.Object, _customerActions.Object);

    private RenewalCommandHandler RenewalHandler()
        => new(_subscriptionService.Object, _renewalService.Object, _renewalProcessor.Object, _customerActions.Object);

    private static async Task ShouldBeDisabled(Func<Task> action)
    {
        (await action.Should().ThrowAsync<ExecutionError>()).Which.Code.Should().Be(ActionDisabledCode);
    }

    [Fact]
    public async Task Service_UsesTheDefaults_WhenTheStoreIsNotFound()
    {
        var storeService = new Mock<IStoreService>();
        storeService.Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync([]);

        var actions = await new SubscriptionCustomerActionsService(storeService.Object).GetAsync(StoreId);

        actions.Should().BeEquivalentTo(new SubscriptionCustomerActions
        {
            CanSkip = false,
            CanOrderNow = false,
            CanChangePaymentMethod = false,
            CanPause = true,
            CanChangeFrequency = true,
        });
    }

    [Fact]
    public async Task Service_ReadsTheStoresSettings()
    {
        var store = new Store
        {
            Id = StoreId,
            Settings =
            [
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.CustomerCanSkip.Name, Value = true },
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.CustomerCanOrderNow.Name, Value = true },
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.CustomerCanChangePaymentMethod.Name, Value = true },
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.CustomerCanPause.Name, Value = false },
                new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.CustomerCanChangeFrequency.Name, Value = false },
            ],
        };
        var storeService = new Mock<IStoreService>();
        storeService.Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync([store]);

        var actions = await new SubscriptionCustomerActionsService(storeService.Object).GetAsync(StoreId);

        actions.Should().BeEquivalentTo(new SubscriptionCustomerActions
        {
            CanSkip = true,
            CanOrderNow = true,
            CanChangePaymentMethod = true,
            CanPause = false,
            CanChangeFrequency = false,
        });
    }

    [Fact]
    public void Settings_ArePublic_SoTheStorefrontCanRead()
    {
        new[]
        {
            ModuleConstants.Settings.Store.CustomerCanSkip,
            ModuleConstants.Settings.Store.CustomerCanOrderNow,
            ModuleConstants.Settings.Store.CustomerCanChangePaymentMethod,
            ModuleConstants.Settings.Store.CustomerCanPause,
            ModuleConstants.Settings.Store.CustomerCanChangeFrequency,
        }.Should().OnlyContain(x => x.IsPublic).And.OnlyContain(x => ModuleConstants.Settings.Store.AllStoreSettings.Contains(x));
    }

    [Fact]
    public async Task Skip_IsRefused_WhenTheStoreTurnedItOff()
    {
        Allow(new SubscriptionCustomerActions { CanSkip = false });

        await ShouldBeDisabled(() => SubscriptionHandler().Handle(new SkipProductSubscriptionCommand { SubscriptionId = SubscriptionId, UserId = UserId }, CancellationToken.None));
        await ShouldBeDisabled(() => RenewalHandler().Handle(new SkipSubscriptionRenewalCommand { RenewalId = RenewalId, UserId = UserId }, CancellationToken.None));

        _subscriptionManager.Verify(x => x.SkipNextAsync(It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never);
        _renewalProcessor.Verify(x => x.SkipAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Skip_Works_WhenTheStoreAllowsIt()
    {
        Allow(new SubscriptionCustomerActions { CanSkip = true });

        await SubscriptionHandler().Handle(new SkipProductSubscriptionCommand { SubscriptionId = SubscriptionId, UserId = UserId }, CancellationToken.None);
        await RenewalHandler().Handle(new SkipSubscriptionRenewalCommand { RenewalId = RenewalId, UserId = UserId }, CancellationToken.None);

        _subscriptionManager.Verify(x => x.SkipNextAsync(SubscriptionId, It.IsAny<DateTime?>()), Times.Once);
        _renewalProcessor.Verify(x => x.SkipAsync(RenewalId), Times.Once);
    }

    [Fact]
    public async Task OrderNow_IsRefused_WhenTheStoreTurnedItOff()
    {
        Allow(new SubscriptionCustomerActions { CanOrderNow = false });

        await ShouldBeDisabled(() => RenewalHandler().Handle(new OrderProductSubscriptionNowCommand { SubscriptionId = SubscriptionId, UserId = UserId }, CancellationToken.None));

        _renewalProcessor.Verify(x => x.OrderNowAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Pause_IsRefused_ButResumeStillWorks_WhenTheStoreTurnedPauseOff()
    {
        Allow(new SubscriptionCustomerActions { CanPause = false });

        await ShouldBeDisabled(() => SubscriptionHandler().Handle(new PauseProductSubscriptionCommand { SubscriptionId = SubscriptionId, UserId = UserId }, CancellationToken.None));
        await SubscriptionHandler().Handle(new ResumeProductSubscriptionCommand { SubscriptionId = SubscriptionId, UserId = UserId }, CancellationToken.None);

        _subscriptionManager.Verify(x => x.PauseAsync(It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never);
        _subscriptionManager.Verify(x => x.ResumeAsync(SubscriptionId), Times.Once);
    }

    [Fact]
    public async Task ChangeFrequency_IsRefused_WhenTheStoreTurnedItOff()
    {
        Allow(new SubscriptionCustomerActions { CanChangeFrequency = false, CanChangePaymentMethod = true });

        await ShouldBeDisabled(() => SubscriptionHandler().Handle(new ChangeProductSubscriptionCommand
        {
            SubscriptionId = SubscriptionId,
            UserId = UserId,
            IntervalUnit = SubscriptionIntervalUnit.Month,
            IntervalCount = 1,
        }, CancellationToken.None));

        _subscriptionManager.Verify(x => x.ChangeAsync(It.IsAny<string>(), It.IsAny<SubscriptionChange>()), Times.Never);
    }

    [Fact]
    public async Task ChangePaymentMethod_IsRefused_WhenTheStoreTurnedItOff()
    {
        Allow(new SubscriptionCustomerActions { CanChangeFrequency = true, CanChangePaymentMethod = false });

        await ShouldBeDisabled(() => SubscriptionHandler().Handle(new ChangeProductSubscriptionCommand
        {
            SubscriptionId = SubscriptionId,
            UserId = UserId,
            SavedPaymentMethodId = "card-2",
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Change_OfQuantityOrUnchangedValues_NeedsNoFlag()
    {
        Allow(new SubscriptionCustomerActions());

        await SubscriptionHandler().Handle(new ChangeProductSubscriptionCommand
        {
            SubscriptionId = SubscriptionId,
            UserId = UserId,
            Quantity = 3,
            IntervalUnit = SubscriptionIntervalUnit.Week,
            IntervalCount = 1,
            SavedPaymentMethodId = "card-1",
        }, CancellationToken.None);

        _subscriptionManager.Verify(x => x.ChangeAsync(SubscriptionId, It.Is<SubscriptionChange>(c => c.Quantity == 3)), Times.Once);
    }
}
