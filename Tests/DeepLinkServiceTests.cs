using NodeTie.Infrastructure;
using Xunit;

namespace NodeTie.Tests;

public sealed class DeepLinkServiceTests
{
    [Fact]
    public void DecodeWinLinkPath_ConvertsCustomSchemeIntoWindowsPath()
    {
        string encoded = "winlink:///C%3A%2FTemp%2FExample.txt";

        string decoded = WinLinkUriCodec.DecodeLink(encoded);

        Assert.Equal("C:\\Temp\\Example.txt", decoded);
    }

    [Fact]
    public void BuildProtocolCommand_QuotesExecutableAndArgumentPlaceholder()
    {
        string exePath = @"C:\Program Files\NodeTie\NodeTie.exe";

        string command = WinLinkProtocolRegistrationService.BuildCommand(exePath);

        Assert.Equal("\"C:\\Program Files\\NodeTie\\NodeTie.exe\" \"%1\"", command);
    }

    [Fact]
    public void EncodeMarkdownLink_UsesFileNameAsLinkText()
    {
        string markdown = WinLinkUriCodec.EncodeMarkdownLink(@"C:\Docs\Target.txt");

        Assert.Equal("[Target.txt](winlink:///C%3A%2FDocs%2FTarget.txt)", markdown);
    }

    [Fact]
    public void EncodeMarkdownLink_EscapesClosingBracketInLabel()
    {
        string markdown = WinLinkUriCodec.EncodeMarkdownLink(@"C:\Docs\A]B.txt");

        Assert.Equal("[A\\]B.txt](winlink:///C%3A%2FDocs%2FA%5DB.txt)", markdown);
    }

    [Fact]
    public void EncodeHtmlLink_UsesFileNameAsLinkText()
    {
        string html = WinLinkUriCodec.EncodeHtmlLink(@"C:\Docs\Target.txt");

        Assert.Equal("<a href=\"winlink:///C%3A%2FDocs%2FTarget.txt\">Target.txt</a>", html);
    }

    [Fact]
    public void EncodeHtmlLink_EscapesHtmlSensitiveCharactersInLabel()
    {
        string html = WinLinkUriCodec.EncodeHtmlLink(@"C:\Docs\A&B<1>.txt");

        Assert.Equal("<a href=\"winlink:///C%3A%2FDocs%2FA%26B%3C1%3E.txt\">A&amp;B&lt;1&gt;.txt</a>", html);
    }

    [Fact]
    public void EncodePath_WithStableId_AppendsSidQueryParameter()
    {
        string encoded = WinLinkUriCodec.EncodePath(@"C:\Docs\Target.txt", "AAAA0001:0000000000001001");

        Assert.Equal("winlink:///C%3A%2FDocs%2FTarget.txt?sid=AAAA0001%3A0000000000001001", encoded);
    }

    [Fact]
    public void TryDecodeLink_WithStableIdQuery_ExtractsPathAndStableId()
    {
        string encoded = "winlink:///C%3A%2FTemp%2FExample.txt?sid=AAAA0001%3A0000000000001001";

        bool ok = WinLinkUriCodec.TryDecodeLink(encoded, out string path, out string? stableId);

        Assert.True(ok);
        Assert.Equal(@"C:\Temp\Example.txt", path);
        Assert.Equal("AAAA0001:0000000000001001", stableId);
    }

    [Fact]
    public void TryDecodeLink_WithWebUrlPath_PreservesHttpsTarget()
    {
        string encoded = "winlink:///https%3A%2F%2Fd.docs.live.net%2F4B8544510772040F%2FDocuments%2FCompanies%2FParafrenalia%2FPartilha%20Andre%2F8.%20CLIENTS%2Faow.cmmsportal.com%2Freport%20template.docx";

        bool ok = WinLinkUriCodec.TryDecodeLink(encoded, out string path, out string? stableId);

        Assert.True(ok);
        Assert.Equal("https://d.docs.live.net/4B8544510772040F/Documents/Companies/Parafrenalia/Partilha Andre/8. CLIENTS/aow.cmmsportal.com/report template.docx", path);
        Assert.Null(stableId);
    }

    [Fact]
    public void TryDecodeLink_WithOneNoteDecodedWebPath_PreservesHttpsTarget()
    {
        string encoded = "winlink:///https://www.pinterest.com/pin-creation-tool/";

        bool ok = WinLinkUriCodec.TryDecodeLink(encoded, out string path, out string? stableId);

        Assert.True(ok);
        Assert.Equal("https://www.pinterest.com/pin-creation-tool/", path);
        Assert.Null(stableId);
    }

    [Fact]
    public void TryDecodeLink_WithOneNoteDecodedPathAndSid_ExtractsStableId()
    {
        string encoded = "winlink:///C:/Users/prime/OneDrive/Desktop/Rui J Alves.docx?sid=82A6EE0C:0053000000043084";

        bool ok = WinLinkUriCodec.TryDecodeLink(encoded, out string path, out string? stableId);

        Assert.True(ok);
        Assert.Equal(@"C:\Users\prime\OneDrive\Desktop\Rui J Alves.docx", path);
        Assert.Equal("82A6EE0C:0053000000043084", stableId);
    }
}
