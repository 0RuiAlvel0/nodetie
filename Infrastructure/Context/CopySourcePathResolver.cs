using System;
using System.Collections.Generic;
using System.Linq;
using NodeTie.Infrastructure.Explorer;

namespace NodeTie.Infrastructure.Context;

public sealed class CopySourcePathResolver
{
    private readonly ActiveFileContextService _activeFileContextService;
    private readonly IExplorerSelectionService _explorerSelectionService;

    public CopySourcePathResolver(
        ActiveFileContextService activeFileContextService,
        IExplorerSelectionService explorerSelectionService)
    {
        _activeFileContextService = activeFileContextService;
        _explorerSelectionService = explorerSelectionService;
    }

    public bool TryResolvePathsForCopy(out IReadOnlyList<string> paths, out string source, out string errorMessage)
    {
        paths = Array.Empty<string>();
        source = string.Empty;
        errorMessage = string.Empty;

        if (_activeFileContextService.TryGetActiveFile(out var activeContext, out errorMessage)
            && activeContext is not null)
        {
            if (string.Equals(activeContext.Source, "Explorer", StringComparison.OrdinalIgnoreCase)
                && _explorerSelectionService.TryGetSelectedPaths(out IReadOnlyList<string> selectedPaths)
                && selectedPaths.Count > 0)
            {
                paths = selectedPaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                paths = [activeContext.Path];
            }

            source = activeContext.Source;
            return true;
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "No active file context was found.";
        }

        return false;
    }
}
