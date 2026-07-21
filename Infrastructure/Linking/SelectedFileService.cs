using System;
using System.IO;
using NodeTie.Infrastructure.Context;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;

namespace NodeTie.Infrastructure.Linking;

public sealed class SelectedFileService
{
    private readonly ActiveFileContextService _activeFileContextService;
    private readonly IFileIdentityService _fileIdentityService;
    private readonly FileRepository _fileRepository;

    public SelectedFileService(
        ActiveFileContextService activeFileContextService,
        IFileIdentityService fileIdentityService,
        FileRepository fileRepository)
    {
        _activeFileContextService = activeFileContextService;
        _fileIdentityService = fileIdentityService;
        _fileRepository = fileRepository;
    }

    public bool TryGetOrCreateSelectedFile(out FileRecord? selectedFile, out string errorMessage)
    {
        selectedFile = null;
        errorMessage = string.Empty;

        if (!_activeFileContextService.TryGetActiveFile(out var activeContext, out errorMessage)
            || activeContext is null)
        {
            return false;
        }

        string selectedPath = activeContext.Path;
        string displayName = Path.GetFileName(selectedPath);
        string stableId = BuildStableId(selectedPath);

        long fileId = _fileRepository.UpsertByStableId(stableId, selectedPath, displayName);
        selectedFile = _fileRepository.GetById(fileId);

        if (selectedFile is null)
        {
            errorMessage = "The selected file could not be loaded from storage.";
            return false;
        }

        return true;
    }

    private string BuildStableId(string path)
    {
        if (_fileIdentityService.TryGetIdentity(path, out FileIdentity identity))
        {
            return identity.ToStableId();
        }

        // Fallback keeps link features usable even when identity lookup cannot be performed.
        return $"PATH:{path.ToUpperInvariant()}";
    }
}
