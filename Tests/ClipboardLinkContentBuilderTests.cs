using NodeTie.Infrastructure.Hotkeys;
using NodeTie.Infrastructure.Linking;
using Xunit;

namespace NodeTie.Tests;

public sealed class ClipboardLinkContentBuilderTests
{
    [Fact]
    public void Build_ForObsidian_ReturnsMarkdownTextOnly()
    {
        ClipboardLinkContent content = ClipboardLinkContentBuilder.Build(
            new[] { ("C:\\Docs\\Target.txt", "winlink:///C%3A%2FDocs%2FTarget.txt") },
            CopyLinkTarget.Obsidian);

        Assert.Equal("[Target.txt](winlink:///C%3A%2FDocs%2FTarget.txt)", content.PlainText);
        Assert.Null(content.HtmlText);
    }

    [Fact]
    public void Build_ForOneNote_ReturnsHtmlOnly()
    {
        ClipboardLinkContent content = ClipboardLinkContentBuilder.Build(
            new[] { ("C:\\Docs\\Target.txt", "winlink:///C%3A%2FDocs%2FTarget.txt") },
            CopyLinkTarget.OneNote);

        Assert.Equal("Target.txt", content.PlainText);
        Assert.Equal("<a href=\"winlink:///C%3A%2FDocs%2FTarget.txt\">Target.txt</a>", content.HtmlText);
    }
}