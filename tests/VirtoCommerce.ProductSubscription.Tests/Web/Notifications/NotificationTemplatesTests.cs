using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Web.Notifications;

/// <summary>
/// Every email template (each language) parses as Liquid, the way the Notifications module renders it, and renders
/// with its notification's model without unknown variables.
/// </summary>
public class NotificationTemplatesTests
{
    private static readonly Regex FileName = new(@"^(?<type>\w+)_(?<part>body|subject)(\.(?<lang>[a-z]{2}(-[A-Za-z]{2})?))?\.(html|txt)$");

    public static TheoryData<string> TemplateFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(TemplatesPath).Select(Path.GetFileName).Order())
        {
            data.Add(file);
        }

        return data;
    }

    [Fact]
    public void Every_notification_has_a_body_and_a_subject_in_every_language_of_its_templates()
    {
        var files = Directory.EnumerateFiles(TemplatesPath).Select(Path.GetFileName).ToList();
        var parsed = files.Select(x => FileName.Match(x)).ToList();
        parsed.Should().OnlyContain(x => x.Success, "every file follows {Type}_{body|subject}[.{lang}].{ext}");

        var languages = parsed.Select(x => x.Groups["lang"].Value).Distinct().ToList();
        foreach (var type in parsed.Select(x => x.Groups["type"].Value).Distinct())
        {
            foreach (var language in languages)
            {
                foreach (var part in new[] { "body", "subject" })
                {
                    parsed.Should().Contain(x => x.Groups["type"].Value == type && x.Groups["part"].Value == part && x.Groups["lang"].Value == language,
                        $"{type} needs its {part} in '{(language == "" ? "default" : language)}'");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TemplateFiles))]
    public async Task Template_parses_as_liquid_and_renders_with_its_model(string fileName)
    {
        var match = FileName.Match(fileName);
        match.Success.Should().BeTrue();

        var notificationType = typeof(SubscriptionEmailNotificationBase).Assembly.GetType(
            $"{typeof(SubscriptionEmailNotificationBase).Namespace}.{match.Groups["type"].Value}");
        notificationType.Should().NotBeNull($"{fileName} belongs to a notification class");

        var template = Template.ParseLiquid(await File.ReadAllTextAsync(Path.Combine(TemplatesPath, fileName), TestContext.Current.CancellationToken));
        template.HasErrors.Should().BeFalse(string.Join("; ", template.Messages));

        var model = CreateModel(notificationType);
        var context = new LiquidTemplateContext
        {
            StrictVariables = true,
            TemplateLoaderLexerOptions = new LexerOptions { Lang = ScriptLang.Liquid, Mode = ScriptMode.Default },
        };
        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        context.PushGlobal(scriptObject);

        var output = await template.RenderAsync(context);

        output.Should().NotContain("{{").And.NotContain("{%");
        if (match.Groups["part"].Value == "body")
        {
            output.Should().Contain("Epson WorkForce WF-3640").And.Contain("SUB-000209").And.Contain("Shop");
        }
    }

    public static TheoryData<string> ReviewRequiredBodies()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(TemplatesPath, "RenewalReviewRequiredEmailNotification_body*.html"))
        {
            data.Add(Path.GetFileName(file));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ReviewRequiredBodies))]
    public async Task A_changed_configuration_lists_the_options_and_marks_the_missing_one(string fileName)
    {
        var template = Template.ParseLiquid(await File.ReadAllTextAsync(Path.Combine(TemplatesPath, fileName), TestContext.Current.CancellationToken));
        var model = CreateModel(typeof(RenewalReviewRequiredEmailNotification));
        model.Renewal.ReviewReasonCode = RenewalReviewReasonCode.ConfigurationChanged;
        model.Lines[0].ConfigurationLines = new List<SubscriptionEmailConfigurationLine>
        {
            new() { Text = "Drinks: Fanta Mango × 6" },
            new() { Text = "Snacks: Pretzels × 2", IsAvailable = false },
        };
        var context = new LiquidTemplateContext
        {
            StrictVariables = true,
            TemplateLoaderLexerOptions = new LexerOptions { Lang = ScriptLang.Liquid, Mode = ScriptMode.Default },
        };
        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        context.PushGlobal(scriptObject);

        // the texts are escaped in the email (× becomes &#215;)
        var output = System.Net.WebUtility.HtmlDecode(await template.RenderAsync(context));

        output.Should().Contain("Drinks: Fanta Mango × 6").And.Contain("Snacks: Pretzels × 2");
        // the missing option is marked, the available one isn't
        output.Should().MatchRegex(@"Pretzels × 2 <span style=""color:#b91c1c"">\(");
        output.Should().NotMatchRegex(@"Fanta Mango × 6 <span");
        output.Should().NotContain("{{").And.NotContain("{%");
    }

    private static SubscriptionEmailNotificationBase CreateModel(Type type)
    {
        var model = (SubscriptionEmailNotificationBase)Activator.CreateInstance(type);
        model.FirstName = "Alla";
        model.CustomerName = "Alla Volkova";
        model.StoreName = "Shop";
        model.StoreUrl = "https://shop.example.com/";
        model.LogoUrl = string.Empty;
        model.AccentColor = "#1f2933";
        model.ManageUrl = "https://shop.example.com/account/subscriptions?subscription=s1";
        model.SubscriptionsUrl = "https://shop.example.com/account/subscriptions";
        model.OrderUrl = "https://shop.example.com/account/orders/o1";
        model.DeliveryDateText = "November 20, 2026";
        model.CutoffDateText = "November 19, 2026";
        model.NextDeliveryDateText = "December 20, 2026";
        model.TotalText = "$90.00";
        model.CardText = "Visa •••• 4242";
        model.Lines = new List<SubscriptionEmailLine>
        {
            new()
            {
                SubscriptionId = "s1",
                SubscriptionNumber = "SUB-000209",
                ProductName = "Epson WorkForce WF-3640",
                Quantity = 1,
                FrequencyText = "Every month",
                DiscountText = "Save 10%",
                TotalText = "$90.00",
                NextDateText = "December 20, 2026",
                ManageUrl = "https://shop.example.com/account/subscriptions?subscription=s1",
                IsAvailable = false,
                Message = "Only 2 left",
            },
        };
        model.Renewal = new Renewal
        {
            Id = "r1",
            OrderNumber = "CO261120-00007",
            ReviewReasonCode = RenewalReviewReasonCode.PriceIncreased,
            ReviewReason = "The total went up.",
        };
        model.Subscriptions = new List<Subscription>();

        switch (model)
        {
            case RenewalPaymentActionRequiredEmailNotification actionRequired:
                actionRequired.PaymentUrl = "https://shop.example.com/account/orders/o1/payment";
                break;
            case SubscriptionPausedEmailNotification paused:
                paused.PausedUntilText = string.Empty;
                break;
        }

        return model;
    }

    private static string TemplatesPath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the tests run inside the repository");
            return Path.Combine(directory!.FullName, "src", "VirtoCommerce.ProductSubscription.Web", "NotificationTemplates");
        }
    }
}
