using NodeTie.Infrastructure.Context.Browser;
using Xunit;

namespace NodeTie.Tests;

public sealed class BrowserUrlResolverTests
{
    [Fact]
    public void TrySelectUrl_ReturnsFirstAbsoluteHttpUrl()
    {
        bool ok = BrowserUrlResolver.TrySelectUrl([
            "search query",
            "https://example.com/path/page"
        ], out string url);

        Assert.True(ok);
        Assert.Equal("https://example.com/path/page", url);
    }

    [Fact]
    public void TrySelectUrl_NormalizesFileUrlToLocalPath()
    {
        bool ok = BrowserUrlResolver.TrySelectUrl([
            "file:///C:/Docs/Report.docx"
        ], out string url);

        Assert.True(ok);
        Assert.Equal(@"C:\Docs\Report.docx", url);
    }
}