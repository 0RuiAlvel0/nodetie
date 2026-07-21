using System;
using System.IO;
using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Tests;

public sealed class SqliteTestDatabase : IDisposable
{
    public string DatabasePath { get; }
    public SqliteConnectionFactory ConnectionFactory { get; }

    public SqliteTestDatabase()
    {
        string fileName = $"nodetie-tests-{Guid.NewGuid():N}.db";
        DatabasePath = Path.Combine(Path.GetTempPath(), fileName);
        ConnectionFactory = new SqliteConnectionFactory(DatabasePath);

        var initializer = new NodeTieDatabaseInitializer(ConnectionFactory);
        initializer.Initialize();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }
        }
        catch
        {
            // Best-effort cleanup for temp files created during test runs.
        }
    }
}
