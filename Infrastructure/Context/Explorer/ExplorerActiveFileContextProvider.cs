using NodeTie.Infrastructure.Explorer;

namespace NodeTie.Infrastructure.Context.Explorer;

public sealed class ExplorerActiveFileContextProvider : IActiveFileContextProvider
{
    private readonly IExplorerSelectionService _explorerSelectionService;
    private readonly IForegroundWindowService _foregroundWindowService;

    public ExplorerActiveFileContextProvider(
        IExplorerSelectionService explorerSelectionService,
        IForegroundWindowService foregroundWindowService)
    {
        _explorerSelectionService = explorerSelectionService;
        _foregroundWindowService = foregroundWindowService;
    }

    public bool TryGetActiveFile(out ActiveFileContext? context, out string errorMessage)
    {
        context = null;
        errorMessage = string.Empty;

        if (!_foregroundWindowService.TryGetForegroundProcessName(out string processName)
            || !string.Equals(processName, "explorer", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!_explorerSelectionService.TryGetSelectedPath(out string path))
        {
            errorMessage = "No file(s) selected.";
            return false;
        }

        context = new ActiveFileContext(path, "Explorer");
        return true;
    }
}
