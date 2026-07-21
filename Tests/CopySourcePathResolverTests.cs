using System.Collections.Generic;
using NodeTie.Infrastructure.Context;
using NodeTie.Infrastructure.Explorer;
using Xunit;

namespace NodeTie.Tests;

public sealed class CopySourcePathResolverTests
{
    [Fact]
    public void TryResolvePathsForCopy_PrefersExplorerSelectionWhenAvailable()
    {
        var contextService = new ActiveFileContextService([
            new FakeContextProvider(ok: true, context: new ActiveFileContext(@"C:\Docs\Word.docx", "Explorer"), errorMessage: string.Empty)
        ]);
        var explorerSelectionService = new FakeExplorerSelectionService(ok: true, paths: [@"C:\Docs\A.txt", @"C:\Docs\B.txt"]);
        var resolver = new CopySourcePathResolver(contextService, explorerSelectionService);

        bool ok = resolver.TryResolvePathsForCopy(out IReadOnlyList<string> paths, out string source, out string message);

        Assert.True(ok);
        Assert.Equal("Explorer", source);
        Assert.Equal(2, paths.Count);
        Assert.Equal(@"C:\Docs\A.txt", paths[0]);
        Assert.Equal(@"C:\Docs\B.txt", paths[1]);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryResolvePathsForCopy_UsesActiveContextWhenExplorerHasNoSelection()
    {
        var contextService = new ActiveFileContextService([
            new FakeContextProvider(ok: true, context: new ActiveFileContext(@"C:\Docs\Word.docx", "Word"), errorMessage: string.Empty)
        ]);
        var explorerSelectionService = new FakeExplorerSelectionService(ok: false, paths: []);
        var resolver = new CopySourcePathResolver(contextService, explorerSelectionService);

        bool ok = resolver.TryResolvePathsForCopy(out IReadOnlyList<string> paths, out string source, out string message);

        Assert.True(ok);
        Assert.Equal("Word", source);
        Assert.Single(paths);
        Assert.Equal(@"C:\Docs\Word.docx", paths[0]);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryResolvePathsForCopy_ReturnsErrorWhenNoSourceAvailable()
    {
        var contextService = new ActiveFileContextService([
            new FakeContextProvider(ok: false, context: null, errorMessage: "No active file context was found.")
        ]);
        var explorerSelectionService = new FakeExplorerSelectionService(ok: false, paths: []);
        var resolver = new CopySourcePathResolver(contextService, explorerSelectionService);

        bool ok = resolver.TryResolvePathsForCopy(out IReadOnlyList<string> paths, out string source, out string message);

        Assert.False(ok);
        Assert.Empty(paths);
        Assert.Equal(string.Empty, source);
        Assert.Contains("No active file context", message);
    }

    private sealed class FakeContextProvider : IActiveFileContextProvider
    {
        private readonly bool _ok;
        private readonly ActiveFileContext? _context;
        private readonly string _errorMessage;

        public FakeContextProvider(bool ok, ActiveFileContext? context, string errorMessage)
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

    private sealed class FakeExplorerSelectionService : IExplorerSelectionService
    {
        private readonly bool _ok;
        private readonly IReadOnlyList<string> _paths;

        public FakeExplorerSelectionService(bool ok, IReadOnlyList<string> paths)
        {
            _ok = ok;
            _paths = paths;
        }

        public bool TryGetSelectedPath(out string path)
        {
            if (_ok && _paths.Count > 0)
            {
                path = _paths[0];
                return true;
            }

            path = string.Empty;
            return false;
        }

        public bool TryGetSelectedPaths(out IReadOnlyList<string> paths)
        {
            paths = _paths;
            return _ok;
        }
    }
}
