using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;
using Xunit;

namespace NodeTie.Tests;

public sealed class LinkRemovalServiceTests
{
    [Fact]
    public void TryRemoveLink_DeletesExistingUndirectedRelationship()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("R:1", @"C:\Docs\Source.txt", "Source.txt");
        long linkedId = fileRepository.UpsertByStableId("R:2", @"C:\Docs\Linked.txt", "Linked.txt");
        linkRepository.AddOrIgnoreUndirectedLink(sourceId, linkedId);

        var service = new LinkRemovalService(linkRepository);
        bool removed = service.TryRemoveLink(sourceId, linkedId);

        Assert.True(removed);
        Assert.Empty(linkRepository.GetLinkedFileIds(sourceId));
    }
}
