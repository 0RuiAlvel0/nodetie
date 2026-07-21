using System;
using System.Collections.Generic;
using System.IO;
using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Infrastructure.Resolution;

public sealed class FileResolutionService
{
    private readonly FileRepository _fileRepository;
    private readonly IFileIdentityService _identityService;
    private readonly IPathExistenceService _pathExistenceService;
    private readonly IStableFileLocator _stableFileLocator;

    public FileResolutionService(
        FileRepository fileRepository,
        IFileIdentityService identityService,
        IPathExistenceService pathExistenceService,
        IStableFileLocator stableFileLocator)
    {
        _fileRepository = fileRepository;
        _identityService = identityService;
        _pathExistenceService = pathExistenceService;
        _stableFileLocator = stableFileLocator;
    }

    public FileResolutionService(
        FileRepository fileRepository,
        IFileIdentityService identityService,
        IPathExistenceService pathExistenceService)
        : this(fileRepository, identityService, pathExistenceService, new NoopStableFileLocator())
    {
    }

    public FileResolutionResult Resolve(FileRecord record)
    {
        if (PathMatchesStableIdentity(record.CurrentPath, record.StableId))
        {
            _fileRepository.SetMissingStatus(record.Id, isMissing: false);
            return FileResolutionResult.FoundAt(record.CurrentPath, usedAliasPath: false);
        }

        IReadOnlyList<string> aliasPaths = _fileRepository.GetAliasPaths(record.Id);
        foreach (string aliasPath in aliasPaths)
        {
            if (string.Equals(aliasPath, record.CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!PathMatchesStableIdentity(aliasPath, record.StableId))
            {
                continue;
            }

            string displayName = Path.GetFileName(aliasPath);
            _fileRepository.UpdateResolvedPath(record.Id, aliasPath, displayName);
            _fileRepository.SetMissingStatus(record.Id, isMissing: false);
            return FileResolutionResult.FoundAt(aliasPath, usedAliasPath: true);
        }

        if (_stableFileLocator.TryLocate(record.StableId, out string locatedPath))
        {
            string displayName = Path.GetFileName(locatedPath);
            _fileRepository.UpdateResolvedPath(record.Id, locatedPath, displayName);
            _fileRepository.SetMissingStatus(record.Id, isMissing: false);
            return FileResolutionResult.FoundAt(locatedPath, usedAliasPath: true);
        }

        _fileRepository.SetMissingStatus(record.Id, isMissing: true);
        return FileResolutionResult.NotFound;
    }

    private bool PathMatchesStableIdentity(string path, string expectedStableId)
    {
        if (!_pathExistenceService.Exists(path))
        {
            return false;
        }

        // If identity lookup fails we fall back to existence so users can still open files.
        if (!_identityService.TryGetIdentity(path, out FileIdentity identity))
        {
            return true;
        }

        return string.Equals(identity.ToStableId(), expectedStableId, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoopStableFileLocator : IStableFileLocator
    {
        public bool TryLocate(string stableId, out string locatedPath)
        {
            locatedPath = string.Empty;
            return false;
        }
    }
}
