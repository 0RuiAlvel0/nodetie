using NodeTie.Infrastructure;
using Xunit;

namespace NodeTie.Tests;

public sealed class SingleInstanceDeepLinkForwarderTests
{
    [Fact]
    public void TryGetDeepLinkArgument_ReturnsFalse_WhenArgsAreEmpty()
    {
        bool ok = SingleInstanceDeepLinkForwarder.TryGetDeepLinkArgument([], out string deepLink);

        Assert.False(ok);
        Assert.Equal(string.Empty, deepLink);
    }

    [Fact]
    public void TryGetDeepLinkArgument_FindsAndNormalizesQuotedArgument()
    {
        bool ok = SingleInstanceDeepLinkForwarder.TryGetDeepLinkArgument(
            ["--noop", "\"winlink:///C%3A%2FDocs%2FTarget.txt\""],
            out string deepLink);

        Assert.True(ok);
        Assert.Equal("winlink:///C%3A%2FDocs%2FTarget.txt", deepLink);
    }
}