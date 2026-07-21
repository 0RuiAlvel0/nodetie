using System.Collections.Generic;
using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;
using Xunit;

namespace NodeTie.Tests;

public sealed class LinkCompositionServiceTests
{
    [Fact]
    public void TryLinkFromClipboard_CreatesLinkFromNodeTieUri()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("SRC:1", @"C:\Docs\Source.txt", "Source.txt");

        var parser = new ClipboardLinkParser();
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\Docs\Target.txt"] = new(0xAAAA0001, 0x0000000000001001)
        });

        var service = new LinkCompositionService(parser, identity, fileRepository, linkRepository);

        bool ok = service.TryLinkFromClipboard(sourceId, "winlink:///C%3A%2FDocs%2FTarget.txt", out var message);

        Assert.True(ok);
        Assert.Equal(string.Empty, message);
        Assert.Single(linkRepository.GetLinkedFileIds(sourceId));
    }

    [Fact]
    public void TryLinkFromClipboard_RejectsInvalidClipboardText()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);
        long sourceId = fileRepository.UpsertByStableId("SRC:2", @"C:\Docs\Source2.txt", "Source2.txt");

        var parser = new ClipboardLinkParser();
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>());
        var service = new LinkCompositionService(parser, identity, fileRepository, linkRepository);

        bool ok = service.TryLinkFromClipboard(sourceId, "not-a-link", out var message);

        Assert.False(ok);
        Assert.Contains("Clipboard does not contain", message);
    }

    [Fact]
    public void TryLinkManyFromClipboard_CreatesMultipleLinksFromMultilineInput()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("SRC:3", @"C:\Docs\Source3.txt", "Source3.txt");

        var parser = new ClipboardLinkParser();
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\Docs\TargetA.txt"] = new(0xAAAA0002, 0x0000000000001002),
            [@"C:\Docs\TargetB.txt"] = new(0xAAAA0003, 0x0000000000001003)
        });

        var service = new LinkCompositionService(parser, identity, fileRepository, linkRepository);
        string payload = "winlink:///C%3A%2FDocs%2FTargetA.txt\nwinlink:///C%3A%2FDocs%2FTargetB.txt";

        bool ok = service.TryLinkManyFromClipboard(sourceId, payload, out int createdCount, out int skippedCount, out string message);

        Assert.True(ok);
        Assert.Equal(2, createdCount);
        Assert.Equal(0, skippedCount);
        Assert.Contains("Linked 2", message);
        Assert.Equal(2, linkRepository.GetLinkedFileIds(sourceId).Count);
    }

    [Fact]
    public void TryLinkFromClipboard_AcceptsMarkdownLinkWrappingWinLinkUri()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("SRC:4", @"C:\Docs\Source4.txt", "Source4.txt");

        var parser = new ClipboardLinkParser();
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\Docs\TargetMd.txt"] = new(0xAAAA0004, 0x0000000000001004)
        });

        var service = new LinkCompositionService(parser, identity, fileRepository, linkRepository);
        string markdown = "[Target file](winlink:///C%3A%2FDocs%2FTargetMd.txt)";

        bool ok = service.TryLinkFromClipboard(sourceId, markdown, out var message);

        Assert.True(ok);
        Assert.Equal(string.Empty, message);
        Assert.Single(linkRepository.GetLinkedFileIds(sourceId));
    }

    [Fact]
    public void TryLinkManyFromClipboard_AcceptsMixedRawAndMarkdownWinLinks()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("SRC:5", @"C:\Docs\Source5.txt", "Source5.txt");

        var parser = new ClipboardLinkParser();
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\Docs\TargetRaw.txt"] = new(0xAAAA0005, 0x0000000000001005),
            [@"C:\Docs\TargetMd2.txt"] = new(0xAAAA0006, 0x0000000000001006)
        });

        var service = new LinkCompositionService(parser, identity, fileRepository, linkRepository);
        string payload = string.Join("\n", new[]
        {
            "winlink:///C%3A%2FDocs%2FTargetRaw.txt",
            "[Second target](winlink:///C%3A%2FDocs%2FTargetMd2.txt)",
            "not-a-link"
        });

        bool ok = service.TryLinkManyFromClipboard(sourceId, payload, out int createdCount, out int skippedCount, out string message);

        Assert.True(ok);
        Assert.Equal(2, createdCount);
        Assert.Equal(1, skippedCount);
        Assert.Contains("Linked 2", message);
        Assert.Equal(2, linkRepository.GetLinkedFileIds(sourceId).Count);
    }

    [Fact]
    public void TryLinkKnownFiles_CreatesLinksAndSkipsDuplicates()
    {
        using var database = new SqliteTestDatabase();
        var fileRepository = new FileRepository(database.ConnectionFactory);
        var linkRepository = new LinkRepository(database.ConnectionFactory);

        long sourceId = fileRepository.UpsertByStableId("SRC:6", @"C:\Docs\Source6.txt", "Source6.txt");
        long targetA = fileRepository.UpsertByStableId("SRC:7", @"C:\Docs\TargetA.txt", "TargetA.txt");
        long targetB = fileRepository.UpsertByStableId("SRC:8", @"C:\Docs\TargetB.txt", "TargetB.txt");
        linkRepository.AddOrIgnoreUndirectedLink(sourceId, targetA);

        var parser = new ClipboardLinkParser();
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>());
        var service = new LinkCompositionService(parser, identity, fileRepository, linkRepository);

        bool ok = service.TryLinkKnownFiles(sourceId, [targetA, targetB], out int createdCount, out int skippedCount, out string message);

        Assert.True(ok);
        Assert.Equal(1, createdCount);
        Assert.Equal(1, skippedCount);
        Assert.Contains("Linked 1", message);
        Assert.Equal(2, linkRepository.GetLinkedFileIds(sourceId).Count);
    }

    private sealed class FakeIdentityService : IFileIdentityService
    {
        private readonly IReadOnlyDictionary<string, FileIdentity> _identities;

        public FakeIdentityService(IReadOnlyDictionary<string, FileIdentity> identities)
        {
            _identities = identities;
        }

        public bool TryGetIdentity(string path, out FileIdentity identity)
        {
            if (_identities.TryGetValue(path, out identity!))
            {
                return true;
            }

            identity = default!;
            return false;
        }
    }
}
