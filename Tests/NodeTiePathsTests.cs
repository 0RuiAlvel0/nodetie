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
}
