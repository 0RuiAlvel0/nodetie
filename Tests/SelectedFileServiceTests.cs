using System.Collections.Generic;
using NodeTie.Infrastructure.Context;
using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;
using Xunit;

namespace NodeTie.Tests;

public sealed class SelectedFileServiceTests
{
    [Fact]
    public void TryGetOrCreateSelectedFile_UsesIdentityWhenAvailable()
    {
        using var database = new SqliteTestDatabase();
        var repository = new FileRepository(database.ConnectionFactory);

        var selection = CreateContextService(@"C:\Docs\Alpha.txt");
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>
        {
            [@"C:\Docs\Alpha.txt"] = new(0xDEADBEEF, 0x0000000000001010)
        });

        var service = new SelectedFileService(selection, identity, repository);

        bool ok = service.TryGetOrCreateSelectedFile(out var file, out var message);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.Equal("DEADBEEF:0000000000001010", file!.StableId);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryGetOrCreateSelectedFile_UsesPathFallbackWhenIdentityUnavailable()
    {
        using var database = new SqliteTestDatabase();
        var repository = new FileRepository(database.ConnectionFactory);

        var selection = CreateContextService(@"C:\Docs\Beta.txt");
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>());

        var service = new SelectedFileService(selection, identity, repository);

        bool ok = service.TryGetOrCreateSelectedFile(out var file, out _);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.StartsWith("PATH:", file!.StableId);
    }

    [Fact]
    public void TryGetOrCreateSelectedFile_ReturnsErrorWhenNothingSelected()
    {
        using var database = new SqliteTestDatabase();
        var repository = new FileRepository(database.ConnectionFactory);

        var selection = CreateContextService(null);
        var identity = new FakeIdentityService(new Dictionary<string, FileIdentity>());

        var service = new SelectedFileService(selection, identity, repository);

        bool ok = service.TryGetOrCreateSelectedFile(out var file, out var message);

        Assert.False(ok);
        Assert.Null(file);
        Assert.Contains("No active file context", message);
    }

    private static ActiveFileContextService CreateContextService(string? path)
    {
        if (path is null)
        {
            return new ActiveFileContextService([
                new FakeActiveFileContextProvider(ok: false, context: null, errorMessage: "No active file context was found.")
            ]);
        }

        return new ActiveFileContextService([
            new FakeActiveFileContextProvider(ok: true, context: new ActiveFileContext(path, "Explorer"), errorMessage: string.Empty)
        ]);
    }

    private sealed class FakeActiveFileContextProvider : IActiveFileContextProvider
    {
        private readonly bool _ok;
        private readonly ActiveFileContext? _context;
        private readonly string _errorMessage;

        public FakeActiveFileContextProvider(bool ok, ActiveFileContext? context, string errorMessage)
        {
            _ok = ok;
            _context = context;
            _errorMessage = errorMessage;
        }

        public bool TryGetActiveFile(out ActiveFileContext? context, out string errorMessage)
        {
            context = _context;
            errorMessage = _errorMessage;
            return _ok;
        }
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
