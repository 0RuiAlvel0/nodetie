using System.Linq;
using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class BookmarkServiceTests
{
    [Fact]
    public void ToggleBookmark_AddsThenRemovesBookmark()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);
        var service = new BookmarkService(fileRepository, bookmarkRepository);

        long fileId = fileRepository.UpsertByStableId("BS:1", @"C:\Docs\Alpha.txt", "Alpha.txt");

        bool added = service.ToggleBookmark(fileId, "manual", out bool isBookmarkedAfterAdd);
        bool removed = service.ToggleBookmark(fileId, "manual", out bool isBookmarkedAfterRemove);

        Assert.True(added);
        Assert.True(isBookmarkedAfterAdd);
        Assert.True(removed);
        Assert.False(isBookmarkedAfterRemove);
    }

    [Fact]
    public void Query_ReturnsPinnedAndTaggedDisplayData()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);
        var service = new BookmarkService(fileRepository, bookmarkRepository);

        long fileId = fileRepository.UpsertByStableId("BS:2", @"C:\Docs\Plan.md", "Plan.md");
        service.EnsureBookmarked(fileId, "manual");
        service.SetPinned(fileId, true);
        service.AddTag(fileId, "Project");

        var results = service.Query(BookmarkView.Pinned, searchTerm: "plan", tagFilter: "project");

        Assert.Single(results);
        Assert.True(results[0].IsPinned);
        Assert.Contains("project", results[0].Tags);
        Assert.Equal(".md", results[0].FileExtension);
    }

    [Fact]
    public void TouchAccessByKnownPath_UpdatesExistingBookmark()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var bookmarkRepository = new BookmarkRepository(database.ConnectionFactory);
        var service = new BookmarkService(fileRepository, bookmarkRepository);

        long fileId = fileRepository.UpsertByStableId("BS:3", @"C:\Docs\Spec.docx", "Spec.docx");
        service.EnsureBookmarked(fileId, "manual");

        bool touched = service.TouchAccessByKnownPath(@"C:\Docs\Spec.docx");

        Assert.True(touched);
        Assert.Single(service.Query(BookmarkView.Recent, string.Empty, string.Empty));
    }
}
