using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Models;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Pricing;

[Trait("Category", "Unit")]
public class SubscriptionPromoEntriesMiddlewareTests
{
    private const string CartId = "C1";
    private const string LineId = "L1";
    private const string ProductId = "P1";
    private const string PlanId = "plan-1";

    private readonly Mock<ISubscriptionCartItemSearchService> _cartItemSearchServiceMock = new();
    private readonly Mock<ISubscriptionPlanService> _planServiceMock = new();

    private SubscriptionPromoEntriesMiddleware CreateSut() => new(_cartItemSearchServiceMock.Object, _planServiceMock.Object, NullLogger<SubscriptionPromoEntriesMiddleware>.Instance);

    [Fact]
    public async Task Run_CheckoutLineWithSavedChoice_MarksEntryWithPlanDiscount()
    {
        // Arrange
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = PlanId });
        SetupPlans(CreatePlan(SubscriptionDiscountType.Percent, 10m));

        var entry = new ProductPromoEntry { ProductId = ProductId, Owner = null };
        var otherEntry = new ProductPromoEntry { ProductId = "P2", Owner = null };
        var map = CreateMap(CreateCart(), entry, otherEntry);

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().Be(new SubscriptionLineDiscount(PlanId, "Monthly", SubscriptionDiscountType.Percent, 10m));
        SubscriptionPromoAttributes.Get(otherEntry).Should().BeNull();
    }

    [Fact]
    public async Task Run_CheckoutLineWithSavedChoice_MarksCartPromoEntriesToo()
    {
        // Arrange
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = PlanId });
        SetupPlans(CreatePlan(SubscriptionDiscountType.FixedAmount, 2m));

        var cartEntry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(CreateCart());
        map.PromotionEvaluationContext.CartPromoEntries = [cartEntry];

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(cartEntry).Should().Be(new SubscriptionLineDiscount(PlanId, "Monthly", SubscriptionDiscountType.FixedAmount, 2m));
    }

    [Fact]
    public async Task Run_SearchesChoicesByCartId()
    {
        // Arrange
        SetupChoices();
        var map = CreateMap(CreateCart(), new ProductPromoEntry { ProductId = ProductId });

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        _cartItemSearchServiceMock.Verify(
            x => x.SearchAsync(It.Is<SubscriptionCartItemSearchCriteria>(c => c.CartIds.SequenceEqual(new[] { CartId })), It.IsAny<bool>()),
            Times.Once);
        _planServiceMock.Verify(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Run_InactivePlan_MarksNothing()
    {
        // Arrange
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = PlanId });
        var plan = CreatePlan(SubscriptionDiscountType.Percent, 10m);
        plan.IsActive = false;
        SetupPlans(plan);

        var entry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(CreateCart(), entry);

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().BeNull();
        entry.Attributes.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Run_PlanWithDiscountTypeNone_MarksNothing()
    {
        // Arrange
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = PlanId });
        SetupPlans(CreatePlan(SubscriptionDiscountType.None, 10m));

        var entry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(CreateCart(), entry);

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().BeNull();
    }

    [Fact]
    public async Task Run_ChoiceForLineNotInCart_MarksNothing()
    {
        // Arrange
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = "removed-line", ProductId = ProductId, PlanId = PlanId });
        SetupPlans(CreatePlan(SubscriptionDiscountType.Percent, 10m));

        var entry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(CreateCart(), entry);

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().BeNull();
    }

    [Fact]
    public async Task Run_ChoiceWithUnknownPlan_MarksNothing()
    {
        // Arrange
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = "deleted-plan" });
        SetupPlans(CreatePlan(SubscriptionDiscountType.Percent, 10m));

        var entry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(CreateCart(), entry);

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().BeNull();
    }

    [Fact]
    public async Task Run_EntryOwnedByLineItem_MatchedByLineId()
    {
        // Arrange
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = PlanId });
        SetupPlans(CreatePlan(SubscriptionDiscountType.Percent, 10m));

        var cart = CreateCart();
        var entry = new ProductPromoEntry { ProductId = ProductId, Owner = cart.Items.First() };
        var map = CreateMap(cart, entry);

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().NotBeNull();
    }

    [Fact]
    public async Task Run_CartWithoutId_DoesNotQueryDatabase()
    {
        // Arrange
        var cart = CreateCart();
        cart.Id = null;
        var entry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(cart, entry);

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().BeNull();
        _cartItemSearchServiceMock.VerifyNoOtherCalls();
        _planServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_DiscountScopeSet_UsesScopeKeyedByProductIdInsteadOfDatabase()
    {
        // Arrange: a renewal cart (no saved cart id needed).
        var cart = CreateCart();
        cart.Id = null;
        var entry = new ProductPromoEntry { ProductId = ProductId };
        var otherEntry = new ProductPromoEntry { ProductId = "P2" };
        var map = CreateMap(cart, entry, otherEntry);
        var discount = new SubscriptionLineDiscount(PlanId, "Renewal plan", SubscriptionDiscountType.FixedAmount, 3m);

        // Act
        using (SubscriptionDiscountScope.Begin(new Dictionary<string, SubscriptionLineDiscount> { [ProductId] = discount }))
        {
            await CreateSut().Run(map, _ => Task.CompletedTask);
        }

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().Be(discount);
        SubscriptionPromoAttributes.Get(otherEntry).Should().BeNull();
        _cartItemSearchServiceMock.VerifyNoOtherCalls();
        _planServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_DiscountScopeSet_IgnoresSavedChoicesForCart()
    {
        // Arrange: the cart has an id and saved choices, but the scope wins.
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = PlanId });
        SetupPlans(CreatePlan(SubscriptionDiscountType.Percent, 10m));
        var entry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(CreateCart(), entry);
        var discount = new SubscriptionLineDiscount("renewal-plan", "Renewal", SubscriptionDiscountType.Percent, 25m);

        // Act
        using (SubscriptionDiscountScope.Begin(new Dictionary<string, SubscriptionLineDiscount> { [ProductId] = discount }))
        {
            await CreateSut().Run(map, _ => Task.CompletedTask);
        }

        // Assert
        SubscriptionPromoAttributes.Get(entry).Should().Be(discount);
        _cartItemSearchServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void DiscountScope_Dispose_RestoresPreviousScope()
    {
        // Arrange
        var outer = new Dictionary<string, SubscriptionLineDiscount>();
        var inner = new Dictionary<string, SubscriptionLineDiscount>();

        // Act / Assert
        SubscriptionDiscountScope.Current.Should().BeNull();
        using (SubscriptionDiscountScope.Begin(outer))
        {
            SubscriptionDiscountScope.Current.Should().BeSameAs(outer);
            using (SubscriptionDiscountScope.Begin(inner))
            {
                SubscriptionDiscountScope.Current.Should().BeSameAs(inner);
            }

            SubscriptionDiscountScope.Current.Should().BeSameAs(outer);
        }

        SubscriptionDiscountScope.Current.Should().BeNull();
    }

    [Fact]
    public async Task Run_CallsNextBeforeMarkingEntries()
    {
        // Arrange: entries are built by the pipeline steps that run before this one, i.e. inside next().
        SetupChoices(new SubscriptionCartItem { CartId = CartId, LineItemId = LineId, ProductId = ProductId, PlanId = PlanId });
        SetupPlans(CreatePlan(SubscriptionDiscountType.Percent, 10m));
        var map = CreateMap(CreateCart());
        var entry = new ProductPromoEntry { ProductId = ProductId };
        var nextCalls = 0;

        // Act
        await CreateSut().Run(map, x =>
        {
            nextCalls++;
            x.PromotionEvaluationContext.PromoEntries.Add(entry);
            return Task.CompletedTask;
        });

        // Assert
        nextCalls.Should().Be(1);
        SubscriptionPromoAttributes.Get(entry).Should().NotBeNull();
    }

    [Fact]
    public async Task Run_ServiceThrows_DoesNotBreakCartCalculation()
    {
        // Arrange
        _cartItemSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionCartItemSearchCriteria>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var entry = new ProductPromoEntry { ProductId = ProductId };
        var map = CreateMap(CreateCart(), entry);

        // Act
        var act = () => CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        await act.Should().NotThrowAsync();
        SubscriptionPromoAttributes.Get(entry).Should().BeNull();
    }

    [Fact]
    public async Task Run_NoCartOrContext_DoesNothing()
    {
        // Arrange
        var map = new PromotionEvaluationContextCartMap();

        // Act
        await CreateSut().Run(map, _ => Task.CompletedTask);

        // Assert
        _cartItemSearchServiceMock.VerifyNoOtherCalls();
    }

    private void SetupChoices(params SubscriptionCartItem[] choices)
    {
        _cartItemSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionCartItemSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync(new SubscriptionCartItemSearchResult { Results = choices.ToList(), TotalCount = choices.Length });
    }

    private void SetupPlans(params SubscriptionPlan[] plans)
    {
        _planServiceMock
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => plans.Where(p => ids.Contains(p.Id)).ToList());
    }

    private static SubscriptionPlan CreatePlan(SubscriptionDiscountType type, decimal value)
    {
        return new SubscriptionPlan
        {
            Id = PlanId,
            Name = "Monthly",
            IsActive = true,
            DiscountType = type,
            DiscountValue = value,
        };
    }

    private static ShoppingCart CreateCart()
    {
        return new ShoppingCart
        {
            Id = CartId,
            Items = [new LineItem { Id = LineId, ProductId = ProductId, Quantity = 1 }],
        };
    }

    private static PromotionEvaluationContextCartMap CreateMap(ShoppingCart cart, params ProductPromoEntry[] entries)
    {
        return new PromotionEvaluationContextCartMap
        {
            CartAggregate = new TestCartAggregate(cart),
            PromotionEvaluationContext = new PromotionEvaluationContext { PromoEntries = entries.ToList() },
        };
    }

    /// <summary>
    /// The middleware only reads <see cref="CartAggregate.Cart"/>, so the aggregate's services are not needed.
    /// </summary>
    private sealed class TestCartAggregate : CartAggregate
    {
        public TestCartAggregate(ShoppingCart cart)
            : base(null, null, null, null, null, null, null, null, null, null, null, null)
        {
            Cart = cart;
        }
    }
}
