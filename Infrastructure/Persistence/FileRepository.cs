using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeTie.Infrastructure.Persistence;

public sealed class FileRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public FileRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public long UpsertByStableId(string stableId, string currentPath, string displayName)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        string utcNow = DateTime.UtcNow.ToString("O");

        using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = """
            SELECT id, current_path, display_name
            FROM files
            WHERE stable_id = $stableId;
            """;
        selectCommand.Parameters.AddWithValue("$stableId", stableId);

        using var reader = selectCommand.ExecuteReader();
        if (reader.Read())
        {
            long id = reader.GetInt64(0);
            string previousPath = reader.GetString(1);
            string previousName = reader.GetString(2);

            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE files
                SET current_path = $currentPath,
                    display_name = $displayName,
                    is_missing = 0,
                    updated_utc = $updatedUtc
                WHERE id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$currentPath", currentPath);
            updateCommand.Parameters.AddWithValue("$displayName", displayName);
            updateCommand.Parameters.AddWithValue("$updatedUtc", utcNow);
            updateCommand.Parameters.AddWithValue("$id", id);
            updateCommand.ExecuteNonQuery();

            // Preserve old names/paths as aliases for better search and recovery after renames.
            if (!string.Equals(previousPath, currentPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousName, displayName, StringComparison.Ordinal))
            {
                InsertAlias(connection, transaction, id, previousPath, previousName, utcNow);
            }

            transaction.Commit();
            return id;
        }

        reader.Close();

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO files(stable_id, current_path, display_name, is_missing, created_utc, updated_utc)
            VALUES($stableId, $currentPath, $displayName, 0, $createdUtc, $updatedUtc);
            SELECT last_insert_rowid();
            """;
        insertCommand.Parameters.AddWithValue("$stableId", stableId);
        insertCommand.Parameters.AddWithValue("$currentPath", currentPath);
        insertCommand.Parameters.AddWithValue("$displayName", displayName);
        insertCommand.Parameters.AddWithValue("$createdUtc", utcNow);
        insertCommand.Parameters.AddWithValue("$updatedUtc", utcNow);

        long newId = (long)(insertCommand.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert file record."));

        InsertAlias(connection, transaction, newId, currentPath, displayName, utcNow);

        transaction.Commit();
        return newId;
    }

    public FileRecord? GetById(long id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, stable_id, current_path, display_name, is_missing, created_utc, updated_utc
            FROM files
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new FileRecord(
            Id: reader.GetInt64(0),
            StableId: reader.GetString(1),
            CurrentPath: reader.GetString(2),
            DisplayName: reader.GetString(3),
            IsMissing: reader.GetInt64(4) == 1,
            CreatedUtc: reader.GetString(5),
            UpdatedUtc: reader.GetString(6));
    }

    public FileRecord? GetByKnownPath(string path)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var currentPathCommand = connection.CreateCommand();
        currentPathCommand.CommandText = """
            SELECT id, stable_id, current_path, display_name, is_missing, created_utc, updated_utc
            FROM files
            WHERE current_path = $path COLLATE NOCASE
            LIMIT 1;
            """;
        currentPathCommand.Parameters.AddWithValue("$path", path);

        using (var currentPathReader = currentPathCommand.ExecuteReader())
        {
            if (currentPathReader.Read())
            {
                return new FileRecord(
                    Id: currentPathReader.GetInt64(0),
                    StableId: currentPathReader.GetString(1),
                    CurrentPath: currentPathReader.GetString(2),
                    DisplayName: currentPathReader.GetString(3),
                    IsMissing: currentPathReader.GetInt64(4) == 1,
                    CreatedUtc: currentPathReader.GetString(5),
                    UpdatedUtc: currentPathReader.GetString(6));
            }
        }

        using var aliasPathCommand = connection.CreateCommand();
        aliasPathCommand.CommandText = """
            SELECT f.id, f.stable_id, f.current_path, f.display_name, f.is_missing, f.created_utc, f.updated_utc
            FROM file_aliases fa
            INNER JOIN files f ON f.id = fa.file_id
            WHERE fa.alias_path = $path COLLATE NOCASE
            ORDER BY fa.seen_utc DESC
            LIMIT 1;
            """;
        aliasPathCommand.Parameters.AddWithValue("$path", path);

        using var aliasPathReader = aliasPathCommand.ExecuteReader();
        if (!aliasPathReader.Read())
        {
            return null;
        }

        return new FileRecord(
            Id: aliasPathReader.GetInt64(0),
            StableId: aliasPathReader.GetString(1),
            CurrentPath: aliasPathReader.GetString(2),
            DisplayName: aliasPathReader.GetString(3),
            IsMissing: aliasPathReader.GetInt64(4) == 1,
            CreatedUtc: aliasPathReader.GetString(5),
            UpdatedUtc: aliasPathReader.GetString(6));
    }

    public IReadOnlyList<FileRecord> SearchByNameOrPath(string query, int maxResults = 50)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT f.id, f.stable_id, f.current_path, f.display_name, f.is_missing, f.created_utc, f.updated_utc
            FROM files f
            LEFT JOIN file_aliases fa ON fa.file_id = f.id
            WHERE f.display_name LIKE $term
               OR f.current_path LIKE $term
               OR fa.alias_name LIKE $term
               OR fa.alias_path LIKE $term
            ORDER BY f.updated_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$term", $"%{query}%");
        command.Parameters.AddWithValue("$limit", maxResults);

        using var reader = command.ExecuteReader();
        List<FileRecord> records = new();
        while (reader.Read())
        {
            records.Add(new FileRecord(
                Id: reader.GetInt64(0),
                StableId: reader.GetString(1),
                CurrentPath: reader.GetString(2),
                DisplayName: reader.GetString(3),
                IsMissing: reader.GetInt64(4) == 1,
                CreatedUtc: reader.GetString(5),
                UpdatedUtc: reader.GetString(6)));
        }

        return records;
    }

    public IReadOnlyList<FileRecord> GetByIds(IReadOnlyList<long> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        using var connection = _connectionFactory.CreateOpenConnection();

        List<string> placeholders = ids.Select((_, index) => $"$id{index}").ToList();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, stable_id, current_path, display_name, is_missing, created_utc, updated_utc
            FROM files
            WHERE id IN ({string.Join(",", placeholders)})
            ORDER BY display_name COLLATE NOCASE;
            """;

        for (int i = 0; i < ids.Count; i++)
        {
            command.Parameters.AddWithValue(placeholders[i], ids[i]);
        }

        using var reader = command.ExecuteReader();
        List<FileRecord> records = new();
        while (reader.Read())
        {
            records.Add(new FileRecord(
                Id: reader.GetInt64(0),
                StableId: reader.GetString(1),
                CurrentPath: reader.GetString(2),
                DisplayName: reader.GetString(3),
                IsMissing: reader.GetInt64(4) == 1,
                CreatedUtc: reader.GetString(5),
                UpdatedUtc: reader.GetString(6)));
        }

        return records;
    }

    public IReadOnlyList<string> GetAliasPaths(long fileId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT alias_path
            FROM file_aliases
            WHERE file_id = $fileId
            ORDER BY seen_utc DESC;
            """;
        command.Parameters.AddWithValue("$fileId", fileId);

        using var reader = command.ExecuteReader();
        List<string> aliasPaths = new();
        while (reader.Read())
        {
            aliasPaths.Add(reader.GetString(0));
        }

        return aliasPaths;
    }

    public void UpdateResolvedPath(long fileId, string resolvedPath, string displayName)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();
        string utcNow = DateTime.UtcNow.ToString("O");

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            UPDATE files
            SET current_path = $currentPath,
                display_name = $displayName,
                updated_utc = $updatedUtc
            WHERE id = $fileId;
            """;
        updateCommand.Parameters.AddWithValue("$currentPath", resolvedPath);
        updateCommand.Parameters.AddWithValue("$displayName", displayName);
        updateCommand.Parameters.AddWithValue("$updatedUtc", utcNow);
        updateCommand.Parameters.AddWithValue("$fileId", fileId);
        updateCommand.ExecuteNonQuery();

        InsertAlias(connection, transaction, fileId, resolvedPath, displayName, utcNow);
        transaction.Commit();
    }

    public void SetMissingStatus(long fileId, bool isMissing)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE files
            SET is_missing = $isMissing,
                updated_utc = $updatedUtc
            WHERE id = $fileId;
            """;
        command.Parameters.AddWithValue("$isMissing", isMissing ? 1 : 0);
        command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$fileId", fileId);
        command.ExecuteNonQuery();
    }

    private static void InsertAlias(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction, long fileId, string path, string name, string seenUtc)
    {
        using var aliasCommand = connection.CreateCommand();
        aliasCommand.Transaction = transaction;
        aliasCommand.CommandText = """
            INSERT INTO file_aliases(file_id, alias_path, alias_name, seen_utc)
            VALUES($fileId, $aliasPath, $aliasName, $seenUtc);
            """;
        aliasCommand.Parameters.AddWithValue("$fileId", fileId);
        aliasCommand.Parameters.AddWithValue("$aliasPath", path);
        aliasCommand.Parameters.AddWithValue("$aliasName", name);
        aliasCommand.Parameters.AddWithValue("$seenUtc", seenUtc);
        aliasCommand.ExecuteNonQuery();
    }
}
