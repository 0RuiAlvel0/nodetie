using System.Collections.Generic;
using System.IO;
using System.Linq;
using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Infrastructure.Linking;

public sealed class LinkedFileQueryService
{
    private readonly LinkRepository _linkRepository;
    private readonly FileRepository _fileRepository;

    public LinkedFileQueryService(LinkRepository linkRepository, FileRepository fileRepository)
    {
        _linkRepository = linkRepository;
        _fileRepository = fileRepository;
    }

    public IReadOnlyList<LinkDisplayItem> GetLinkedFiles(long sourceFileId)
    {
        IReadOnlyList<long> linkedIds = _linkRepository.GetLinkedFileIds(sourceFileId);
        IReadOnlyList<FileRecord> linkedRecords = _fileRepository.GetByIds(linkedIds);

        List<LinkDisplayItem> displayItems = new();
        foreach (FileRecord record in linkedRecords)
        {
            displayItems.Add(new LinkDisplayItem(
                record.Id,
                record.DisplayName,
                record.CurrentPath,
                GetFileExtension(record.CurrentPath),
                record.UpdatedUtc,
                record.IsMissing));
        }

        return displayItems;
    }

    public IReadOnlyList<LinkDisplayItem> SearchKnownFilesForLinking(long sourceFileId, string query, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        HashSet<long> excludedIds = _linkRepository
            .GetLinkedFileIds(sourceFileId)
            .Append(sourceFileId)
            .ToHashSet();

        IReadOnlyList<FileRecord> records = _fileRepository.SearchByNameOrPath(query, maxResults);
        List<LinkDisplayItem> displayItems = new();
        foreach (FileRecord record in records)
        {
            if (excludedIds.Contains(record.Id))
            {
                continue;
            }

            displayItems.Add(new LinkDisplayItem(
                record.Id,
                record.DisplayName,
                record.CurrentPath,
                GetFileExtension(record.CurrentPath),
                record.UpdatedUtc,
                record.IsMissing));
        }

        return displayItems;
    }

    private static string GetFileExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) ? "(none)" : extension;
    }
}
