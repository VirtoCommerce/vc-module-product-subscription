using FluentAssertions;
using FluentValidation.Results;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.XCart.Core.Models;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>A validation error marks only the renewal line it is about.</summary>
[Trait("Category", "Unit")]
public class RenewalCartLineErrorsTests
{
    private readonly LineItem _coffee = new() { Id = "line-1", ProductId = "coffee" };
    private readonly LineItem _tea = new() { Id = "line-2", ProductId = "tea" };

    [Fact]
    public void ErrorOnOneLine_MarksOnlyThatLine()
    {
        ValidationFailure[] errors = [new CartValidationError(nameof(LineItem), _coffee.Id, "Out of stock")];

        RenewalCartBuilder.GetLineErrors(errors, _coffee, _coffee.ProductId).Should().Equal("Out of stock");
        RenewalCartBuilder.GetLineErrors(errors, _tea, _tea.ProductId).Should().BeEmpty();
    }

    [Fact]
    public void ErrorOnAnObjectWithoutId_MarksNoLine()
    {
        // E.g. the in-memory shipment: it used to match every line, whose ids were empty too.
        ValidationFailure[] errors = [new CartValidationError(nameof(Shipment), null, "No shipping rate")];

        RenewalCartBuilder.GetLineErrors(errors, _coffee, _coffee.ProductId).Should().BeEmpty();
        RenewalCartBuilder.GetLineErrors(errors, new LineItem { ProductId = "tea" }, "tea").Should().BeEmpty();
    }

    [Fact]
    public void ErrorOnTheProduct_MarksItsLine_EvenWhenTheLineWasNotAdded()
    {
        ValidationFailure[] errors = [new CartValidationError("CatalogProduct", "tea", "The product is no longer sold")];

        RenewalCartBuilder.GetLineErrors(errors, null, "tea").Should().Equal("The product is no longer sold");
        RenewalCartBuilder.GetLineErrors(errors, _coffee, _coffee.ProductId).Should().BeEmpty();
    }

    [Fact]
    public void OtherValidationFailures_AreIgnored()
    {
        ValidationFailure[] errors = [new ValidationFailure("Items", "Something else")];

        RenewalCartBuilder.GetLineErrors(errors, _coffee, _coffee.ProductId).Should().BeEmpty();
    }
}
