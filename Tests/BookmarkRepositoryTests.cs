using System.Linq;
using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class BookmarkRepositoryTests
{
    [Fact]
    public void UpsertBookmark_ThenQueryAll_ReturnsBookmarkedRecord()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);

        long fileId = fileRepository.UpsertByStableId("BM:1", @"C:\Docs\Alpha.txt", "Alpha.txt");
        bookmarkRepository.UpsertBookmark(fileId, "manual");

        var results = bookmarkRepository.QueryBookmarks(BookmarkView.All, searchTerm: string.Empty, tagFilter: string.Empty);

        Assert.Single(results);
        Assert.Equal(fileId, results[0].FileId);
        Assert.Equal("Alpha.txt", results[0].DisplayName);
        Assert.False(results[0].IsPinned);
    }

    [Fact]
    public void SetPinned_AndViewPinned_FiltersCorrectly()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);

        long alpha = fileRepository.UpsertByStableId("BM:2", @"C:\Docs\Alpha.txt", "Alpha.txt");
        long beta = fileRepository.UpsertByStableId("BM:3", @"C:\Docs\Beta.txt", "Beta.txt");
        bookmarkRepository.UpsertBookmark(alpha, "manual");
        bookmarkRepository.UpsertBookmark(beta, "manual");
        bookmarkRepository.SetPinned(beta, true);

        var pinned = bookmarkRepository.QueryBookmarks(BookmarkView.Pinned, string.Empty, string.Empty);

        Assert.Single(pinned);
        Assert.Equal(beta, pinned[0].FileId);
        Assert.True(pinned[0].IsPinned);
    }

    [Fact]
    public void AddTag_ThenSearchByTag_ReturnsMatchingBookmark()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);

        long alpha = fileRepository.UpsertByStableId("BM:4", @"C:\Docs\Alpha.txt", "Alpha.txt");
        long beta = fileRepository.UpsertByStableId("BM:5", @"C:\Docs\Beta.txt", "Beta.txt");
        bookmarkRepository.UpsertBookmark(alpha, "manual");
        bookmarkRepository.UpsertBookmark(beta, "manual");
        bookmarkRepository.AddTag(alpha, "Project-X");

        var bySearch = bookmarkRepository.QueryBookmarks(BookmarkView.All, "project-x", string.Empty);
        var byFilter = bookmarkRepository.QueryBookmarks(BookmarkView.All, string.Empty, "project-x");

        Assert.Single(bySearch);
        Assert.Equal(alpha, bySearch[0].FileId);
        Assert.Single(byFilter);
        Assert.Equal(alpha, byFilter[0].FileId);
        Assert.Contains("project-x", byFilter[0].Tags);
    }

    [Fact]
    public void ViewRecent_SortsByLastAccessedDescending()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);

        long alpha = fileRepository.UpsertByStableId("BM:6", @"C:\Docs\Alpha.txt", "Alpha.txt");
        long beta = fileRepository.UpsertByStableId("BM:7", @"C:\Docs\Beta.txt", "Beta.txt");
        bookmarkRepository.UpsertBookmark(alpha, "manual");
        bookmarkRepository.UpsertBookmark(beta, "manual");

        bookmarkRepository.TouchAccess(alpha);
        bookmarkRepository.TouchAccess(beta);

        var recent = bookmarkRepository.QueryBookmarks(BookmarkView.Recent, string.Empty, string.Empty);

        Assert.Equal(2, recent.Count);
        Assert.Equal(beta, recent[0].FileId);
        Assert.Equal(alpha, recent[1].FileId);
    }

    [Fact]
    public void RemoveBookmark_DeletesRecordAndTags()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);

        long fileId = fileRepository.UpsertByStableId("BM:8", @"C:\Docs\Gamma.txt", "Gamma.txt");
        bookmarkRepository.UpsertBookmark(fileId, "manual");
        bookmarkRepository.AddTag(fileId, "work");

        bool removed = bookmarkRepository.RemoveBookmark(fileId);

        Assert.True(removed);
        Assert.False(bookmarkRepository.IsBookmarked(fileId));
        Assert.Empty(bookmarkRepository.GetTags(fileId));
        Assert.Empty(bookmarkRepository.QueryBookmarks(BookmarkView.All, string.Empty, string.Empty));
    }
}
