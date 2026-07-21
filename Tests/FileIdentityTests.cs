using NodeTie.Infrastructure.Resolution;
using Xunit;

namespace NodeTie.Tests;

public sealed class FileIdentityTests
{
    [Fact]
    public void ToStableId_FormatsVolumeAndFileIndexAsUpperHex()
    {
        var identity = new FileIdentity(0x1A2B3C4D, 0x0000000012345678);

        string stableId = identity.ToStableId();

        Assert.Equal("1A2B3C4D:0000000012345678", stableId);
    }
}
