using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class NodeTieDatabaseInitializerTests
{
    [Fact]
    public void Initialize_CreatesExpectedCoreTables()
    {
        using var testDatabase = new SqliteTestDatabase();

        HashSet<string> tableNames = new(StringComparer.OrdinalIgnoreCase);
        using var connection = testDatabase.ConnectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
                            AND name IN ('files', 'file_aliases', 'links', 'settings', 'bookmarks', 'bookmark_tags');
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            tableNames.Add(reader.GetString(0));
        }

        Assert.Contains("files", tableNames);
        Assert.Contains("file_aliases", tableNames);
        Assert.Contains("links", tableNames);
        Assert.Contains("settings", tableNames);
        Assert.Contains("bookmarks", tableNames);
        Assert.Contains("bookmark_tags", tableNames);
    }
}
