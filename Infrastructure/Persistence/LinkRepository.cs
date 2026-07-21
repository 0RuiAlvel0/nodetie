using System;
using System.Collections.Generic;

namespace NodeTie.Infrastructure.Persistence;

public sealed class LinkRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LinkRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public bool AddOrIgnoreUndirectedLink(long firstFileId, long secondFileId, string? note = null)
    {
        if (firstFileId == secondFileId)
        {
            throw new ArgumentException("Cannot link a file to itself.", nameof(secondFileId));
        }

        long fileAId = Math.Min(firstFileId, secondFileId);
        long fileBId = Math.Max(firstFileId, secondFileId);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO links(file_a_id, file_b_id, note, created_utc)
            VALUES($fileAId, $fileBId, $note, $createdUtc);
            """;
        command.Parameters.AddWithValue("$fileAId", fileAId);
        command.Parameters.AddWithValue("$fileBId", fileBId);
        command.Parameters.AddWithValue("$note", note ?? string.Empty);
        command.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));

        int changedRows = command.ExecuteNonQuery();
        return changedRows > 0;
    }

    public IReadOnlyList<long> GetLinkedFileIds(long fileId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                       WHEN file_a_id = $fileId THEN file_b_id
                       ELSE file_a_id
                   END AS linked_file_id
            FROM links
            WHERE file_a_id = $fileId OR file_b_id = $fileId
            ORDER BY linked_file_id;
            """;
        command.Parameters.AddWithValue("$fileId", fileId);

        using var reader = command.ExecuteReader();
        List<long> ids = new();
        while (reader.Read())
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    public bool RemoveUndirectedLink(long firstFileId, long secondFileId)
    {
        if (firstFileId == secondFileId)
        {
            return false;
        }

        long fileAId = Math.Min(firstFileId, secondFileId);
        long fileBId = Math.Max(firstFileId, secondFileId);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM links
            WHERE file_a_id = $fileAId AND file_b_id = $fileBId;
            """;
        command.Parameters.AddWithValue("$fileAId", fileAId);
        command.Parameters.AddWithValue("$fileBId", fileBId);

        return command.ExecuteNonQuery() > 0;
    }
}
