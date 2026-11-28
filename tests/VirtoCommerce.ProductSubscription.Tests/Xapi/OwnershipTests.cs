using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Web.Controllers.Api;
using VirtoCommerce.ProductSubscription.Xapi.Commands;
using VirtoCommerce.ProductSubscription.Xapi.Queries;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Xapi;

/// <summary>A customer can read and change only their own subscriptions, renewals and cart; every admin action needs a permission.</summary>
[Trait("Category", "Unit")]
public class OwnershipTests
{
    private const string Owner = "customer-1";
    private const string Stranger = "customer-2";

    private readonly Mock<ISubscriptionService> _subscriptions = new();
    private readonly Mock<IRenewalService> _renewals = new();
    private readonly Mock<ISubscriptionManager> _manager = new();
    private readonly Mock<IRenewalProcessor> _processor = new();

    public OwnershipTests()
    {
        _subscriptions
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync([new Subscription { Id = "sub-1", CustomerId = Owner, StoreId = "store-1" }]);
        _renewals
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync([new Renewal { Id = "ren-1", CustomerId = Owner, StoreId = "store-1" }]);
    }

    public static TheoryData<string> SubscriptionCommands => new() { "skip", "pause", "resume", "cancel", "change" };

    [Theory]
    [MemberData(nameof(SubscriptionCommands))]
    public async Task AnotherCustomersSubscription_CanNotBeChanged(string command)
    {
        var handler = new ProductSubscriptionCommandHandler(_subscriptions.Object, _manager.Object, Mock.Of<ISubscriptionCustomerActionsService>());

        var act = command switch
        {
            "skip" => () => handler.Handle(new SkipProductSubscriptionCommand { SubscriptionId = "sub-1", UserId = Stranger }, CancellationToken.None),
            "pause" => () => handler.Handle(new PauseProductSubscriptionCommand { SubscriptionId = "sub-1", UserId = Stranger }, CancellationToken.None),
            "resume" => () => handler.Handle(new ResumeProductSubscriptionCommand { SubscriptionId = "sub-1", UserId = Stranger }, CancellationToken.None),
            "cancel" => () => handler.Handle(new CancelProductSubscriptionCommand { SubscriptionId = "sub-1", UserId = Stranger }, CancellationToken.None),
            _ => (System.Func<Task<Subscription>>)(() => handler.Handle(new ChangeProductSubscriptionCommand { SubscriptionId = "sub-1", UserId = Stranger, Quantity = 2 }, CancellationToken.None)),
        };

        await act.Should().ThrowAsync<AuthorizationError>();
        _manager.VerifyNoOtherCalls();
    }

    public static TheoryData<string> RenewalCommands => new() { "order-now", "accept", "skip", "retry" };

    [Theory]
    [MemberData(nameof(RenewalCommands))]
    public async Task AnotherCustomersRenewal_CanNotBeActedOn(string command)
    {
        var handler = new RenewalCommandHandler(_subscriptions.Object, _renewals.Object, _processor.Object, Mock.Of<ISubscriptionCustomerActionsService>());

        var act = command switch
        {
            "order-now" => () => handler.Handle(new OrderProductSubscriptionNowCommand { SubscriptionId = "sub-1", UserId = Stranger }, CancellationToken.None),
            "accept" => () => handler.Handle(new AcceptSubscriptionRenewalCommand { RenewalId = "ren-1", UserId = Stranger }, CancellationToken.None),
            "skip" => () => handler.Handle(new SkipSubscriptionRenewalCommand { RenewalId = "ren-1", UserId = Stranger }, CancellationToken.None),
            _ => (System.Func<Task<Renewal>>)(() => handler.Handle(new RetrySubscriptionRenewalPaymentCommand { RenewalId = "ren-1", UserId = Stranger }, CancellationToken.None)),
        };

        await act.Should().ThrowAsync<AuthorizationError>();
        _processor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnotherCustomersSubscription_CanNotBeRead()
    {
        var handler = new GetProductSubscriptionQueryHandler(_subscriptions.Object);

        var act = () => handler.Handle(new GetProductSubscriptionQuery { Id = "sub-1", UserId = Stranger }, CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationError>();
    }

    [Fact]
    public async Task AnotherCustomersCart_CanNotGetOrLoseASubscription()
    {
        var carts = new Mock<IShoppingCartService>();
        carts
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync([new ShoppingCart { Id = "cart-1", CustomerId = Owner, StoreId = "store-1", Items = [new LineItem { Id = "line-1", ProductId = "coffee", ListPrice = 5 }] }]);
        var cartItems = new Mock<ISubscriptionCartItemService>();
        var set = new SetCartItemSubscriptionCommandHandler(carts.Object, Mock.Of<ISubscriptionOfferService>(), Mock.Of<ISubscriptionCartItemSearchService>(), cartItems.Object, Mock.Of<VirtoCommerce.XCart.Core.Services.ICartAggregateRepository>());
        var remove = new RemoveCartItemSubscriptionCommandHandler(carts.Object, Mock.Of<ISubscriptionCartItemSearchService>(), cartItems.Object);

        var setAct = () => set.Handle(new SetCartItemSubscriptionCommand { CartId = "cart-1", LineItemId = "line-1", PlanId = "plan-1", UserId = Stranger }, CancellationToken.None);
        var removeAct = () => remove.Handle(new RemoveCartItemSubscriptionCommand { CartId = "cart-1", LineItemId = "line-1", UserId = Stranger }, CancellationToken.None);

        await setAct.Should().ThrowAsync<AuthorizationError>();
        await removeAct.Should().ThrowAsync<AuthorizationError>();
        cartItems.VerifyNoOtherCalls();
    }

    [Fact]
    public void EveryAdminAction_NeedsAProductSubscriptionPermission()
    {
        var actions = typeof(ProductSubscriptionController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(x => x.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        actions.Should().NotBeEmpty();
        foreach (var action in actions)
        {
            var policies = action.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ToList();
            policies.Should().Contain(x => x != null && x.StartsWith("product-subscription:"), $"{action.Name} must require a module permission");
        }
    }
}
