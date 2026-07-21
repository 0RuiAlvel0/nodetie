using NodeTie.Infrastructure.Linking;
using Xunit;

namespace NodeTie.Tests;

public sealed class HtmlClipboardFormatterTests
{
    [Fact]
    public void BuildClipboardHtml_IncludesClipboardHeadersAndFragment()
    {
        string html = HtmlClipboardFormatter.BuildClipboardHtml("<a href=\"winlink:///C%3A%2FDocs%2FTarget.txt\">Target.txt</a>");

        Assert.Contains("Version:1.0", html);
        Assert.Contains("StartHTML:", html);
        Assert.Contains("EndHTML:", html);
        Assert.Contains("StartFragment:", html);
        Assert.Contains("EndFragment:", html);
        Assert.Contains("<!--StartFragment-->", html);
        Assert.Contains("<!--EndFragment-->", html);
    }

    [Fact]
    public void BuildClipboardHtml_PreservesProvidedFragment()
    {
        const string fragment = "<a href=\"winlink:///C%3A%2FDocs%2FTarget.txt\">Target.txt</a><br/><a href=\"winlink:///C%3A%2FDocs%2FOther.txt\">Other.txt</a>";
        string html = HtmlClipboardFormatter.BuildClipboardHtml(fragment);

        Assert.Contains(fragment, html);
    }
}