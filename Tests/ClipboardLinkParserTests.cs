using NodeTie.Infrastructure.Linking;
using Xunit;

namespace NodeTie.Tests;

public sealed class ClipboardLinkParserTests
{
    [Fact]
    public void TryParseTargetPath_ExtractsWinLinkFromMarkdown()
    {
        var parser = new ClipboardLinkParser();

        bool ok = parser.TryParseTargetPath("[issues](winlink:///https%3A%2F%2Fgithub.com%2Fowner%2Frepo%2Fissues)", out string path);

        Assert.True(ok);
        Assert.Equal("https://github.com/owner/repo/issues", path);
    }

    [Fact]
    public void TryParseTargetPath_AcceptsHttpUrl()
    {
        var parser = new ClipboardLinkParser();

        bool ok = parser.TryParseTargetPath("https://example.com/docs/page?x=1", out string path);

        Assert.True(ok);
        Assert.Equal("https://example.com/docs/page?x=1", path);
    }

    [Fact]
    public void TryParseTargetPath_ConvertsFileUrlToLocalPath()
    {
        var parser = new ClipboardLinkParser();

        bool ok = parser.TryParseTargetPath("file:///C:/Docs/Target.txt", out string path);

        Assert.True(ok);
        Assert.Equal(@"C:\Docs\Target.txt", path);
    }
}