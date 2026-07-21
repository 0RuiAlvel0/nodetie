namespace NodeTie.Infrastructure.Explorer;

public interface IExplorerSelectionService
{
    bool TryGetSelectedPath(out string path);
    bool TryGetSelectedPaths(out IReadOnlyList<string> paths);
}
