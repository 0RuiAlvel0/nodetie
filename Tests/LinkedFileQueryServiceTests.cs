using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class LinkedFileQueryServiceTests
{
    [Fact]
    public void GetLinkedFiles_ReturnsDisplayItemsForSourceFile()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("S:1", @"C:\Docs\Source.txt", "Source.txt");
        long linkedA = fileRepository.UpsertByStableId("S:2", @"C:\Docs\LinkedA.txt", "LinkedA.txt");
        long linkedB = fileRepository.UpsertByStableId("S:3", @"C:\Docs\LinkedB.txt", "LinkedB.txt");

        linkRepository.AddOrIgnoreUndirectedLink(sourceId, linkedA);
        linkRepository.AddOrIgnoreUndirectedLink(sourceId, linkedB);

        var service = new LinkedFileQueryService(linkRepository, fileRepository);
        var linkedFiles = service.GetLinkedFiles(sourceId);

        Assert.Equal(2, linkedFiles.Count);
        Assert.Contains(linkedFiles, record => record.FileId == linkedA && record.Name == "LinkedA.txt" && record.FileExtension == ".txt");
        Assert.Contains(linkedFiles, record => record.FileId == linkedB && record.Name == "LinkedB.txt" && record.FileExtension == ".txt");
    }

    [Fact]
    public void SearchKnownFilesForLinking_ExcludesSourceAndExistingLinks()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("S:10", @"C:\Docs\Source.txt", "Source.txt");
        long alreadyLinkedId = fileRepository.UpsertByStableId("S:11", @"C:\Docs\Linked.txt", "Linked.txt");
        long candidateId = fileRepository.UpsertByStableId("S:12", @"C:\Docs\Candidate.txt", "Candidate.txt");

        linkRepository.AddOrIgnoreUndirectedLink(sourceId, alreadyLinkedId);

        var service = new LinkedFileQueryService(linkRepository, fileRepository);
        IReadOnlyList<LinkDisplayItem> results = service.SearchKnownFilesForLinking(sourceId, "txt");

        Assert.DoesNotContain(results, file => file.FileId == sourceId);
        Assert.DoesNotContain(results, file => file.FileId == alreadyLinkedId);
        Assert.Contains(results, file => file.FileId == candidateId);
    }
}
