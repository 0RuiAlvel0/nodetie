using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NodeTie.Infrastructure.Explorer;

public sealed class ExplorerSelectionService : IExplorerSelectionService
{
    public bool TryGetSelectedPath(out string path)
    {
        path = string.Empty;
        if (!TryGetSelectedPaths(out IReadOnlyList<string> paths) || paths.Count == 0)
        {
            return false;
        }

        path = paths[0];
        return true;
    }

    public bool TryGetSelectedPaths(out IReadOnlyList<string> paths)
    {
        List<string> selectedPaths = [];
        paths = selectedPaths;

        Type? shellAppType = Type.GetTypeFromProgID("Shell.Application");
        if (shellAppType is null)
        {
            return false;
        }

        dynamic? shellApp = null;
        dynamic? shellWindows = null;

        try
        {
            shellApp = Activator.CreateInstance(shellAppType);
            if (shellApp is null)
            {
                return false;
            }

            shellWindows = shellApp.Windows();
            foreach (var windowObj in shellWindows)
            {
                dynamic window = windowObj;
                string fullName = (window.FullName as string) ?? string.Empty;
                string filename = Path.GetFileNameWithoutExtension(fullName).ToLowerInvariant();
                if (filename != "explorer")
                {
                    continue;
                }

                dynamic document = window.Document;
                if (document is null)
                {
                    continue;
                }

                dynamic selectedItems = document.SelectedItems();
                foreach (var itemObj in selectedItems)
                {
                    dynamic item = itemObj;
                    string selectedPath = (item.Path as string) ?? string.Empty;
                    if (selectedPath.Length == 0)
                    {
                        continue;
                    }

                    selectedPaths.Add(selectedPath);
                }
            }

            paths = selectedPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return paths.Count > 0;
        }
        finally
        {
            if (shellWindows is not null && Marshal.IsComObject(shellWindows))
            {
                Marshal.FinalReleaseComObject(shellWindows);
            }

            if (shellApp is not null && Marshal.IsComObject(shellApp))
            {
                Marshal.FinalReleaseComObject(shellApp);
            }
        }
    }
}
