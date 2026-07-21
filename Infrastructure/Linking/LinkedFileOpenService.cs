using System;
using System.Diagnostics;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;

namespace NodeTie.Infrastructure.Linking;

public sealed class LinkedFileOpenService
{
    private readonly FileRepository _fileRepository;
    private readonly FileResolutionService _fileResolutionService;
    private readonly BookmarkService? _bookmarkService;

    public LinkedFileOpenService(
        FileRepository fileRepository,
        FileResolutionService fileResolutionService,
        BookmarkService? bookmarkService = null)
    {
        _fileRepository = fileRepository;
        _fileResolutionService = fileResolutionService;
        _bookmarkService = bookmarkService;
    }

    public bool TryOpenFile(long fileId, out string message)
    {
        message = string.Empty;

        FileRecord? fileRecord = _fileRepository.GetById(fileId);
        if (fileRecord is null)
        {
            message = "The linked file no longer exists in the database.";
            return false;
        }

        // This is the resolver wiring: file opens only after current path/alias resolution succeeds.
        FileResolutionResult resolution = _fileResolutionService.Resolve(fileRecord);
        if (!resolution.Found || string.IsNullOrWhiteSpace(resolution.ResolvedPath))
        {
            message = $"Could not resolve '{fileRecord.DisplayName}'.";
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = resolution.ResolvedPath,
            UseShellExecute = true
        });

        _bookmarkService?.TouchAccess(fileId);

        return true;
    }
}
