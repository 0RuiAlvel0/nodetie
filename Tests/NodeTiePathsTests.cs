using NodeTie.Infrastructure;
using Xunit;

namespace NodeTie.Tests;

public sealed class NodeTiePathsTests
{
    [Fact]
    public void GetDatabasePath_EndsWithExpectedFileName()
    {
        string databasePath = NodeTiePaths.GetDatabasePath();

        Assert.EndsWith("nodetie.db", databasePath, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetLatestInstallerVersion_ReturnsHighestSemanticVersionFolder()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"NodeTieTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "artifacts", "installer", "0.0.4"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "artifacts", "installer", "1.2.0"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "artifacts", "installer", "1.10.0"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "artifacts", "installer", "notes"));

        try
        {
            bool resolved = NodeTieVersionResolver.TryGetLatestInstallerVersion(tempRoot, out string version);

            Assert.True(resolved);
            Assert.Equal("1.10.0", version);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3+abc123", "1.2.3")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    public void TryNormalizeVersion_ParsesReleaseFormats(string input, string expected)
    {
        bool normalized = NodeTieVersionResolver.TryNormalizeVersion(input, out string version);

        Assert.True(normalized);
        Assert.Equal(expected, version);
    }
}
