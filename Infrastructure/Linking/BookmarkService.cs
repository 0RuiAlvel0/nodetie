using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Infrastructure.Linking;

public sealed class BookmarkService
{
    private readonly FileRepository _fileRepository;
    private readonly BookmarkRepository _bookmarkRepository;

    public BookmarkService(FileRepository fileRepository, BookmarkRepository bookmarkRepository)
    {
        _fileRepository = fileRepository;
        _bookmarkRepository = bookmarkRepository;
    }

    public bool EnsureBookmarked(long fileId, string source)
    {
        return _bookmarkRepository.UpsertBookmark(fileId, source);
    }

    public bool ToggleBookmark(long fileId, string source, out bool isBookmarked)
    {
        if (_bookmarkRepository.IsBookmarked(fileId))
        {
            isBookmarked = false;
            return _bookmarkRepository.RemoveBookmark(fileId);
        }

        isBookmarked = true;
        return _bookmarkRepository.UpsertBookmark(fileId, source);
    }

    public bool IsBookmarked(long fileId)
    {
        return _bookmarkRepository.IsBookmarked(fileId);
    }

    public bool RemoveBookmark(long fileId)
    {
        return _bookmarkRepository.RemoveBookmark(fileId);
    }

    public bool IsPinned(long fileId)
    {
        return _bookmarkRepository.IsPinned(fileId);
    }

    public bool SetPinned(long fileId, bool isPinned)
    {
        return _bookmarkRepository.SetPinned(fileId, isPinned);
    }

    public bool TogglePinned(long fileId)
    {
        bool isPinned = _bookmarkRepository.IsPinned(fileId);
        return _bookmarkRepository.SetPinned(fileId, !isPinned);
    }

    public bool TouchAccess(long fileId)
    {
        return _bookmarkRepository.TouchAccess(fileId);
    }

    public bool TouchAccessByKnownPath(string path)
    {
        FileRecord? file = _fileRepository.GetByKnownPath(path);
        return file is not null && _bookmarkRepository.TouchAccess(file.Id);
    }

    public bool AddTag(long fileId, string tag)
    {
        return _bookmarkRepository.AddTag(fileId, tag);
    }

    public bool RemoveTag(long fileId, string tag)
    {
        return _bookmarkRepository.RemoveTag(fileId, tag);
    }

    public IReadOnlyList<string> GetTags(long fileId)
    {
        return _bookmarkRepository.GetTags(fileId);
    }

    public IReadOnlyList<BookmarkedLinkDisplayItem> Query(
        BookmarkView view,
        string? searchTerm,
        string? tagFilter,
        int maxResults = 200)
    {
        IReadOnlyList<BookmarkedFileRecord> records = _bookmarkRepository.QueryBookmarks(view, searchTerm, tagFilter, maxResults);

        List<BookmarkedLinkDisplayItem> displayItems = new();
        foreach (BookmarkedFileRecord record in records)
        {
            displayItems.Add(new BookmarkedLinkDisplayItem(
                FileId: record.FileId,
                Name: record.DisplayName,
                Path: record.CurrentPath,
                FileExtension: GetFileExtension(record.CurrentPath),
                LastSeenUtc: record.UpdatedUtc,
                IsMissing: record.IsMissing,
                IsPinned: record.IsPinned,
                LastAccessedUtc: record.LastAccessedUtc,
                Tags: record.Tags));
        }

        return displayItems;
    }

    private static string GetFileExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) ? "(none)" : extension;
    }
}
