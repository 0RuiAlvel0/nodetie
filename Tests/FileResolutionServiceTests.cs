using System.Collections.Generic;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;
using Xunit;

namespace NodeTie.Tests;

public sealed class FileResolutionServiceTests
{
    [Fact]
    public void Resolve_WhenCurrentPathMatchesIdentity_ReturnsCurrentPath()
    {
        using var database = new SqliteTestDatabase();
        var repository = new FileRepository(database.ConnectionFactory);

        long fileId = repository.UpsertByStableId("AAAA0001:0000000000000001", @"C:\Current.txt", "Current.txt");
        FileRecord record = repository.GetById(fileId)!;

        var pathService = new FakePathExistenceService(new[] { @"C:\Current.txt" });
        var identityService = new FakeFileIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\Current.txt"] = new(0xAAAA0001, 0x0000000000000001)
        });

        var resolver = new FileResolutionService(repository, identityService, pathService, new FakeStableFileLocator(string.Empty));
        FileResolutionResult result = resolver.Resolve(record);

        Assert.True(result.Found);
        Assert.Equal(@"C:\Current.txt", result.ResolvedPath);
        Assert.False(result.UsedAliasPath);

        FileRecord updated = repository.GetById(fileId)!;
        Assert.False(updated.IsMissing);
    }

    [Fact]
    public void Resolve_WhenCurrentPathMissing_UsesAliasAndUpdatesCurrentPath()
    {
        using var database = new SqliteTestDatabase();
        var repository = new FileRepository(database.ConnectionFactory);

        // First path becomes alias history after the second upsert updates current_path.
        long fileId = repository.UpsertByStableId("BBBB0002:0000000000000002", @"C:\OldPath.txt", "OldPath.txt");
        repository.UpsertByStableId("BBBB0002:0000000000000002", @"C:\MissingNow.txt", "MissingNow.txt");

        FileRecord record = repository.GetById(fileId)!;

        var pathService = new FakePathExistenceService(new[] { @"C:\OldPath.txt" });
        var identityService = new FakeFileIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\OldPath.txt"] = new(0xBBBB0002, 0x0000000000000002)
        });

        var resolver = new FileResolutionService(repository, identityService, pathService, new FakeStableFileLocator(string.Empty));
        FileResolutionResult result = resolver.Resolve(record);

        Assert.True(result.Found);
        Assert.True(result.UsedAliasPath);
        Assert.Equal(@"C:\OldPath.txt", result.ResolvedPath);

        FileRecord updated = repository.GetById(fileId)!;
        Assert.Equal(@"C:\OldPath.txt", updated.CurrentPath);
        Assert.Equal("OldPath.txt", updated.DisplayName);
        Assert.False(updated.IsMissing);
    }

    [Fact]
    public void Resolve_WhenNoPathCanBeResolved_MarksFileMissing()
    {
        using var database = new SqliteTestDatabase();
        var repository = new FileRepository(database.ConnectionFactory);

        long fileId = repository.UpsertByStableId("CCCC0003:0000000000000003", @"C:\Lost.txt", "Lost.txt");
        FileRecord record = repository.GetById(fileId)!;

        var pathService = new FakePathExistenceService([]);
        var identityService = new FakeFileIdentityService(new Dictionary<string, FileIdentity>());

        var resolver = new FileResolutionService(repository, identityService, pathService, new FakeStableFileLocator(string.Empty));
        FileResolutionResult result = resolver.Resolve(record);

        Assert.False(result.Found);
        Assert.Null(result.ResolvedPath);

        FileRecord updated = repository.GetById(fileId)!;
        Assert.True(updated.IsMissing);
    }

    [Fact]
    public void Resolve_WhenFileMoved_UsesStableIdLocatorToRecoverPath()
    {
        using var database = new SqliteTestDatabase();
        var repository = new FileRepository(database.ConnectionFactory);

        long fileId = repository.UpsertByStableId("DDDD0004:0000000000000004", @"C:\OldFolder\Moved.txt", "Moved.txt");
        FileRecord record = repository.GetById(fileId)!;

        var pathService = new FakePathExistenceService([]);
        var identityService = new FakeFileIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\NewFolder\Moved.txt"] = new(0xDDDD0004, 0x0000000000000004)
        });
        var locator = new FakeStableFileLocator(@"C:\NewFolder\Moved.txt");

        var resolver = new FileResolutionService(repository, identityService, pathService, locator);
        FileResolutionResult result = resolver.Resolve(record);

        Assert.True(result.Found);
        Assert.Equal(@"C:\NewFolder\Moved.txt", result.ResolvedPath);

        FileRecord updated = repository.GetById(fileId)!;
        Assert.Equal(@"C:\NewFolder\Moved.txt", updated.CurrentPath);
        Assert.False(updated.IsMissing);
    }

    private sealed class FakePathExistenceService : IPathExistenceService
    {
        private readonly HashSet<string> _existingPaths;

        public FakePathExistenceService(IEnumerable<string> existingPaths)
        {
            _existingPaths = new HashSet<string>(existingPaths, System.StringComparer.OrdinalIgnoreCase);
        }

        public bool Exists(string path)
        {
            return _existingPaths.Contains(path);
        }
    }

    private sealed class FakeFileIdentityService : IFileIdentityService
    {
        private readonly IReadOnlyDictionary<string, FileIdentity> _identityByPath;

        public FakeFileIdentityService(IReadOnlyDictionary<string, FileIdentity> identityByPath)
        {
            _identityByPath = identityByPath;
        }

        public bool TryGetIdentity(string path, out FileIdentity identity)
        {
            if (_identityByPath.TryGetValue(path, out identity!))
            {
                return true;
            }

            identity = default!;
            return false;
        }
    }

    private sealed class FakeStableFileLocator : IStableFileLocator
    {
        private readonly string _locatedPath;

        public FakeStableFileLocator(string locatedPath)
        {
            _locatedPath = locatedPath;
        }

        public bool TryLocate(string stableId, out string locatedPath)
        {
            locatedPath = _locatedPath;
            return !string.IsNullOrWhiteSpace(_locatedPath);
        }
    }
}
