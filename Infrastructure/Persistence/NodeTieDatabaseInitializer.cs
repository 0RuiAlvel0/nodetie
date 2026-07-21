using System;

namespace NodeTie.Infrastructure.Persistence;

public sealed class NodeTieDatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public NodeTieDatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        // Core schema for files, aliases, links, and settings lives here.
        using var schemaCommand = connection.CreateCommand();
        schemaCommand.Transaction = transaction;
        schemaCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                stable_id TEXT NOT NULL UNIQUE,
                current_path TEXT NOT NULL,
                display_name TEXT NOT NULL,
                is_missing INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS file_aliases (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_id INTEGER NOT NULL,
                alias_path TEXT NOT NULL,
                alias_name TEXT NOT NULL,
                seen_utc TEXT NOT NULL,
                FOREIGN KEY(file_id) REFERENCES files(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS links (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_a_id INTEGER NOT NULL,
                file_b_id INTEGER NOT NULL,
                note TEXT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY(file_a_id) REFERENCES files(id) ON DELETE CASCADE,
                FOREIGN KEY(file_b_id) REFERENCES files(id) ON DELETE CASCADE,
                CHECK(file_a_id < file_b_id),
                UNIQUE(file_a_id, file_b_id)
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS bookmarks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_id INTEGER NOT NULL UNIQUE,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                source TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                last_accessed_utc TEXT NOT NULL,
                FOREIGN KEY(file_id) REFERENCES files(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS bookmark_tags (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_id INTEGER NOT NULL,
                tag TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY(file_id) REFERENCES files(id) ON DELETE CASCADE,
                UNIQUE(file_id, tag)
            );

            CREATE INDEX IF NOT EXISTS idx_file_aliases_file_id ON file_aliases(file_id);
            CREATE INDEX IF NOT EXISTS idx_file_aliases_alias_name ON file_aliases(alias_name);
            CREATE INDEX IF NOT EXISTS idx_file_aliases_alias_path ON file_aliases(alias_path);
            CREATE INDEX IF NOT EXISTS idx_links_file_a_id ON links(file_a_id);
            CREATE INDEX IF NOT EXISTS idx_links_file_b_id ON links(file_b_id);
            CREATE INDEX IF NOT EXISTS idx_bookmarks_file_id ON bookmarks(file_id);
            CREATE INDEX IF NOT EXISTS idx_bookmarks_is_pinned ON bookmarks(is_pinned);
            CREATE INDEX IF NOT EXISTS idx_bookmarks_last_accessed_utc ON bookmarks(last_accessed_utc);
            CREATE INDEX IF NOT EXISTS idx_bookmark_tags_file_id ON bookmark_tags(file_id);
            CREATE INDEX IF NOT EXISTS idx_bookmark_tags_tag ON bookmark_tags(tag);
            """;

        schemaCommand.ExecuteNonQuery();
        transaction.Commit();
    }
}
