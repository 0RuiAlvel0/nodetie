using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeTie.Infrastructure.Persistence;

public sealed class BookmarkRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public BookmarkRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public bool UpsertBookmark(long fileId, string source)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        string utcNow = DateTime.UtcNow.ToString("O");

        command.CommandText = """
            INSERT INTO bookmarks(file_id, is_pinned, source, created_utc, updated_utc, last_accessed_utc)
            VALUES($fileId, 0, $source, $utcNow, $utcNow, $utcNow)
            ON CONFLICT(file_id) DO UPDATE
            SET source = excluded.source,
                updated_utc = excluded.updated_utc,
                last_accessed_utc = excluded.last_accessed_utc;
            """;
        command.Parameters.AddWithValue("$fileId", fileId);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$utcNow", utcNow);

        return command.ExecuteNonQuery() > 0;
    }

    public bool RemoveBookmark(long fileId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteTags = connection.CreateCommand())
        {
            deleteTags.Transaction = transaction;
            deleteTags.CommandText = "DELETE FROM bookmark_tags WHERE file_id = $fileId;";
            deleteTags.Parameters.AddWithValue("$fileId", fileId);
            deleteTags.ExecuteNonQuery();
        }

        int deletedRows;
        using (var deleteBookmark = connection.CreateCommand())
        {
            deleteBookmark.Transaction = transaction;
            deleteBookmark.CommandText = "DELETE FROM bookmarks WHERE file_id = $fileId;";
            deleteBookmark.Parameters.AddWithValue("$fileId", fileId);
            deletedRows = deleteBookmark.ExecuteNonQuery();
        }

        transaction.Commit();
        return deletedRows > 0;
    }

    public bool IsBookmarked(long fileId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM bookmarks WHERE file_id = $fileId LIMIT 1;";
        command.Parameters.AddWithValue("$fileId", fileId);
        return command.ExecuteScalar() is not null;
    }

    public bool IsPinned(long fileId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_pinned FROM bookmarks WHERE file_id = $fileId LIMIT 1;";
        command.Parameters.AddWithValue("$fileId", fileId);

        object? value = command.ExecuteScalar();
        return value is long longValue && longValue == 1;
    }

    public bool SetPinned(long fileId, bool isPinned)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE bookmarks
            SET is_pinned = $isPinned,
                updated_utc = $utcNow
            WHERE file_id = $fileId;
            """;
        command.Parameters.AddWithValue("$fileId", fileId);
        command.Parameters.AddWithValue("$isPinned", isPinned ? 1 : 0);
        command.Parameters.AddWithValue("$utcNow", DateTime.UtcNow.ToString("O"));

        return command.ExecuteNonQuery() > 0;
    }

    public bool TouchAccess(long fileId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE bookmarks
            SET last_accessed_utc = $utcNow,
                updated_utc = $utcNow
            WHERE file_id = $fileId;
            """;
        command.Parameters.AddWithValue("$fileId", fileId);
        command.Parameters.AddWithValue("$utcNow", DateTime.UtcNow.ToString("O"));

        return command.ExecuteNonQuery() > 0;
    }

    public bool AddTag(long fileId, string tag)
    {
        string normalized = NormalizeTag(tag);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO bookmark_tags(file_id, tag, created_utc)
            VALUES($fileId, $tag, $createdUtc);
            """;
        command.Parameters.AddWithValue("$fileId", fileId);
        command.Parameters.AddWithValue("$tag", normalized);
        command.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
        return command.ExecuteNonQuery() > 0;
    }

    public bool RemoveTag(long fileId, string tag)
    {
        string normalized = NormalizeTag(tag);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bookmark_tags WHERE file_id = $fileId AND tag = $tag;";
        command.Parameters.AddWithValue("$fileId", fileId);
        command.Parameters.AddWithValue("$tag", normalized);
        return command.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<string> GetTags(long fileId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tag
            FROM bookmark_tags
            WHERE file_id = $fileId
            ORDER BY tag COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$fileId", fileId);

        using var reader = command.ExecuteReader();
        List<string> tags = new();
        while (reader.Read())
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    public IReadOnlyList<BookmarkedFileRecord> QueryBookmarks(
        BookmarkView view,
        string? searchTerm,
        string? tagFilter,
        int maxResults = 200)
    {
        string search = searchTerm?.Trim() ?? string.Empty;
        string tag = NormalizeTag(tagFilter ?? string.Empty);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT f.id,
                   f.stable_id,
                   f.current_path,
                   f.display_name,
                   f.is_missing,
                   f.created_utc,
                   f.updated_utc,
                   b.is_pinned,
                   b.last_accessed_utc
            FROM bookmarks b
            INNER JOIN files f ON f.id = b.file_id
            WHERE ($viewAll = 1
                OR ($viewPinned = 1 AND b.is_pinned = 1)
                OR ($viewRecent = 1))
              AND ($search = ''
                OR f.display_name LIKE $searchLike
                OR f.current_path LIKE $searchLike
                OR EXISTS (
                    SELECT 1
                    FROM bookmark_tags st
                    WHERE st.file_id = b.file_id
                      AND st.tag LIKE $searchLike
                ))
              AND ($tag = ''
                OR EXISTS (
                    SELECT 1
                    FROM bookmark_tags tf
                    WHERE tf.file_id = b.file_id
                      AND tf.tag LIKE $tagLike
                ))
            ORDER BY
                CASE WHEN $viewRecent = 1 THEN b.last_accessed_utc END DESC,
                CASE WHEN $viewRecent = 0 THEN b.is_pinned END DESC,
                CASE WHEN $viewRecent = 0 THEN b.updated_utc END DESC,
                f.display_name COLLATE NOCASE ASC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue("$viewAll", view == BookmarkView.All ? 1 : 0);
        command.Parameters.AddWithValue("$viewPinned", view == BookmarkView.Pinned ? 1 : 0);
        command.Parameters.AddWithValue("$viewRecent", view == BookmarkView.Recent ? 1 : 0);
        command.Parameters.AddWithValue("$search", search);
        command.Parameters.AddWithValue("$searchLike", $"%{search}%");
        command.Parameters.AddWithValue("$tag", tag);
        command.Parameters.AddWithValue("$tagLike", $"%{tag}%");
        command.Parameters.AddWithValue("$limit", maxResults);

        using var reader = command.ExecuteReader();
        List<BookmarkedFileRecord> records = new();
        List<long> fileIds = new();
        while (reader.Read())
        {
            long fileId = reader.GetInt64(0);
            fileIds.Add(fileId);
            records.Add(new BookmarkedFileRecord(
                FileId: fileId,
                StableId: reader.GetString(1),
                CurrentPath: reader.GetString(2),
                DisplayName: reader.GetString(3),
                IsMissing: reader.GetInt64(4) == 1,
                CreatedUtc: reader.GetString(5),
                UpdatedUtc: reader.GetString(6),
                IsPinned: reader.GetInt64(7) == 1,
                LastAccessedUtc: reader.GetString(8),
                Tags: []));
        }

        if (records.Count == 0)
        {
            return records;
        }

        Dictionary<long, IReadOnlyList<string>> tagsByFileId = GetTagsByFileIds(connection, fileIds);
        return records
            .Select(record => record with
            {
                Tags = tagsByFileId.TryGetValue(record.FileId, out IReadOnlyList<string>? tags)
                    ? tags
                    : []
            })
            .ToList();
    }

    private static Dictionary<long, IReadOnlyList<string>> GetTagsByFileIds(Microsoft.Data.Sqlite.SqliteConnection connection, IReadOnlyList<long> fileIds)
    {
        if (fileIds.Count == 0)
        {
            return [];
        }

        List<string> placeholders = fileIds.Select((_, index) => $"$id{index}").ToList();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT file_id, tag
            FROM bookmark_tags
            WHERE file_id IN ({string.Join(",", placeholders)})
            ORDER BY tag COLLATE NOCASE;
            """;
        for (int index = 0; index < fileIds.Count; index++)
        {
            command.Parameters.AddWithValue(placeholders[index], fileIds[index]);
        }

        using var reader = command.ExecuteReader();
        Dictionary<long, List<string>> mutable = new();
        while (reader.Read())
        {
            long fileId = reader.GetInt64(0);
            string tag = reader.GetString(1);
            if (!mutable.TryGetValue(fileId, out List<string>? tags))
            {
                tags = new List<string>();
                mutable[fileId] = tags;
            }

            tags.Add(tag);
        }

        return mutable.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }

    private static string NormalizeTag(string tag)
    {
        return (tag ?? string.Empty).Trim().ToLowerInvariant();
    }
}
