using NodeTie.Infrastructure.Context;
using NodeTie.Infrastructure.Context.Explorer;
using NodeTie.Infrastructure.Explorer;
using Xunit;

namespace NodeTie.Tests;

public sealed class ExplorerActiveFileContextProviderTests
{
    [Fact]
    public void TryGetActiveFile_ReturnsFalse_WhenExplorerIsNotForeground()
    {
        var selection = new FakeExplorerSelectionService(@"C:\Docs\A.txt", hasSelection: true);
        var foreground = new FakeForegroundWindowService("msedge");
        var provider = new ExplorerActiveFileContextProvider(selection, foreground);

        bool ok = provider.TryGetActiveFile(out ActiveFileContext? context, out string message);

        Assert.False(ok);
        Assert.Null(context);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryGetActiveFile_ReturnsSelection_WhenExplorerIsForeground()
    {
        var selection = new FakeExplorerSelectionService(@"C:\Docs\A.txt", hasSelection: true);
        var foreground = new FakeForegroundWindowService("explorer");
        var provider = new ExplorerActiveFileContextProvider(selection, foreground);

        bool ok = provider.TryGetActiveFile(out ActiveFileContext? context, out string message);

        Assert.True(ok);
        Assert.NotNull(context);
        Assert.Equal(@"C:\Docs\A.txt", context!.Path);
        Assert.Equal("Explorer", context.Source);
        Assert.Equal(string.Empty, message);
    }

    private sealed class FakeExplorerSelectionService : IExplorerSelectionService
    {
        private readonly string _path;
        private readonly bool _hasSelection;

        public FakeExplorerSelectionService(string path, bool hasSelection)
        {
            _path = path;
            _hasSelection = hasSelection;
        }

        public bool TryGetSelectedPath(out string path)
        {
            path = _path;
            return _hasSelection;
        }

        public bool TryGetSelectedPaths(out IReadOnlyList<string> paths)
        {
            if (_hasSelection)
            {
                paths = [_path];
                return true;
            }

            paths = [];
            return false;
        }
    }

    private sealed class FakeForegroundWindowService : IForegroundWindowService
    {
        private readonly string _processName;

        public FakeForegroundWindowService(string processName)
        {
            _processName = processName;
        }

        public bool TryGetForegroundProcessName(out string processName)
        {
            processName = _processName;
            return true;
        }
    }
}
