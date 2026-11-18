using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Services;

public class SubscriptionTermsServiceTests
{
    [Fact]
    public async Task Default_terms_are_markdown_paragraphs_with_bold_lead_ins()
    {
        var storeService = new Mock<IStoreService>();
        var service = new SubscriptionTermsService(storeService.Object);

        var terms = await service.GetTermsAsync("unknown-store");

        terms.Text.Should().Be(ModuleConstants.Settings.DefaultTermsText);
        terms.Text.Split("\n\n").Should().HaveCount(3)
            .And.SatisfyRespectively(
                x => x.Should().StartWith("**Automatic renewal.**"),
                x => x.Should().StartWith("**Changes and cancellation.**"),
                x => x.Should().StartWith("**Price changes.**"));
        terms.Version.Should().Be("2026-11");
        terms.Hash.Should().Be(SubscriptionTermsService.ComputeHash(terms.Text));
    }
}
