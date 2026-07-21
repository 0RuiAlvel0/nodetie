using System;
using System.Collections.Generic;
using System.Linq;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;

namespace NodeTie.Infrastructure.Linking;

public sealed class LinkCompositionService
{
    private readonly ClipboardLinkParser _linkParser;
    private readonly IFileIdentityService _fileIdentityService;
    private readonly FileRepository _fileRepository;
    private readonly LinkRepository _linkRepository;

    public LinkCompositionService(
        ClipboardLinkParser linkParser,
        IFileIdentityService fileIdentityService,
        FileRepository fileRepository,
        LinkRepository linkRepository)
    {
        _linkParser = linkParser;
        _fileIdentityService = fileIdentityService;
        _fileRepository = fileRepository;
        _linkRepository = linkRepository;
    }

    public bool TryLinkFromClipboard(long sourceFileId, string clipboardText, out string message)
    {
        bool ok = TryLinkManyFromClipboard(sourceFileId, clipboardText, out int createdCount, out int skippedCount, out string batchMessage);
        message = batchMessage;
        if (!ok)
        {
            return false;
        }

        // Preserve the previous contract: single-link path reports an empty success message.
        if (createdCount == 1 && skippedCount == 0)
        {
            message = string.Empty;
        }

        return true;
    }

    public bool TryLinkManyFromClipboard(long sourceFileId, string clipboardText, out int createdCount, out int skippedCount, out string message)
    {
        createdCount = 0;
        skippedCount = 0;
        message = string.Empty;

        string[] lines = clipboardText
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            message = "Clipboard does not contain a valid file path or NodeTie link.";
            return false;
        }

        HashSet<string> uniqueTargets = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            if (!_linkParser.TryParseTargetPath(line, out string targetPath))
            {
                skippedCount++;
                continue;
            }

            uniqueTargets.Add(targetPath);
        }

        if (uniqueTargets.Count == 0)
        {
            message = "Clipboard does not contain a valid file path or NodeTie link.";
            return false;
        }

        foreach (string targetPath in uniqueTargets)
        {
            string targetStableId = BuildStableId(targetPath);
            string targetDisplayName = System.IO.Path.GetFileName(targetPath);
            long targetFileId = _fileRepository.UpsertByStableId(targetStableId, targetPath, targetDisplayName);

            if (sourceFileId == targetFileId)
            {
                skippedCount++;
                continue;
            }

            bool created = _linkRepository.AddOrIgnoreUndirectedLink(sourceFileId, targetFileId);
            if (created)
            {
                createdCount++;
            }
            else
            {
                skippedCount++;
            }
        }

        if (createdCount == 0)
        {
            message = "No new links were created from the pasted text.";
            return false;
        }

        if (skippedCount == 0)
        {
            message = createdCount == 1
                ? "Linked 1 file."
                : $"Linked {createdCount} files.";
            return true;
        }

        message = $"Linked {createdCount} file(s); skipped {skippedCount}.";
        return true;
    }

    public bool TryLinkKnownFiles(long sourceFileId, IReadOnlyList<long> targetFileIds, out int createdCount, out int skippedCount, out string message)
    {
        createdCount = 0;
        skippedCount = 0;
        message = string.Empty;

        foreach (long targetFileId in targetFileIds.Distinct())
        {
            if (targetFileId == sourceFileId)
            {
                skippedCount++;
                continue;
            }

            bool created = _linkRepository.AddOrIgnoreUndirectedLink(sourceFileId, targetFileId);
            if (created)
            {
                createdCount++;
            }
            else
            {
                skippedCount++;
            }
        }

        if (createdCount == 0)
        {
            message = "No new links were created.";
            return false;
        }

        message = skippedCount == 0
            ? (createdCount == 1 ? "Linked 1 file." : $"Linked {createdCount} files.")
            : $"Linked {createdCount} file(s); skipped {skippedCount}.";
        return true;
    }

    private string BuildStableId(string path)
    {
        if (_fileIdentityService.TryGetIdentity(path, out FileIdentity identity))
        {
            return identity.ToStableId();
        }

        return $"PATH:{path.ToUpperInvariant()}";
    }
}
