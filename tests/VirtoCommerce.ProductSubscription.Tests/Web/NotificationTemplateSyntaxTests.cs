using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Web;

/// <summary>
/// The platform parses notification templates as Liquid (LiquidTemplateRenderer: Template.ParseLiquid). A Scriban block
/// such as {{ if }} … {{ end }} fails there, and the email goes out with an empty subject, body and recipient.
/// </summary>
[Trait("Category", "Unit")]
public partial class NotificationTemplateSyntaxTests
{
    [Fact]
    public void Every_template_uses_liquid_tags_for_blocks()
    {
        var folder = FindTemplatesFolder();
        var files = Directory.GetFiles(folder);
        files.Should().NotBeEmpty();

        var offenders = files
            .SelectMany(file => ScribanBlock().Matches(File.ReadAllText(file)).Select(match => $"{Path.GetFileName(file)}: {match.Value}"))
            .ToList();

        offenders.Should().BeEmpty("blocks must be written as {% if %} … {% endif %}, {% for %} … {% endfor %}");
    }

    private static string FindTemplatesFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "VirtoCommerce.ProductSubscription.Web", "NotificationTemplates");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("NotificationTemplates folder not found above the test output");
    }

    [GeneratedRegex(@"\{\{-?\s*(if|else|elsif|end|for|unless|case|when|while)\b[^}]*\}\}")]
    private static partial Regex ScribanBlock();
}
