using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class LinkRepositoryTests
{
    [Fact]
    public void AddOrIgnoreUndirectedLink_CreatesSingleRelationshipAndAvoidsDuplicates()
    {
        using var testDatabase = new SqliteTestDatabase();
        var fileRepository = new FileRepository(testDatabase.ConnectionFactory);
        var linkRepository = new LinkRepository(testDatabase.ConnectionFactory);

        long first = fileRepository.UpsertByStableId("volumeA:1001", @"C:\Docs\A.txt", "A.txt");
        long second = fileRepository.UpsertByStableId("volumeA:1002", @"C:\Docs\B.txt", "B.txt");

        bool created = linkRepository.AddOrIgnoreUndirectedLink(first, second, "example link");
        bool duplicateIgnored = linkRepository.AddOrIgnoreUndirectedLink(second, first, "duplicate");

        Assert.True(created);
        Assert.False(duplicateIgnored);

        var linkedFromFirst = linkRepository.GetLinkedFileIds(first);
        var linkedFromSecond = linkRepository.GetLinkedFileIds(second);

        Assert.Single(linkedFromFirst);
        Assert.Single(linkedFromSecond);
        Assert.Equal(second, linkedFromFirst[0]);
        Assert.Equal(first, linkedFromSecond[0]);
    }

    [Fact]
    public void AddOrIgnoreUndirectedLink_ThrowsOnSelfLink()
    {
        using var testDatabase = new SqliteTestDatabase();
        var fileRepository = new FileRepository(testDatabase.ConnectionFactory);
        var linkRepository = new LinkRepository(testDatabase.ConnectionFactory);

        long fileId = fileRepository.UpsertByStableId("volumeA:2001", @"C:\Docs\C.txt", "C.txt");

        Assert.Throws<System.ArgumentException>(() => linkRepository.AddOrIgnoreUndirectedLink(fileId, fileId));
    }
}
