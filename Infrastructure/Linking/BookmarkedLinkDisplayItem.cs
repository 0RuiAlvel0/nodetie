using System.Collections.Generic;

namespace NodeTie.Infrastructure.Linking;

public sealed record BookmarkedLinkDisplayItem(
    long FileId,
    string Name,
    string Path,
    string FileExtension,
    string LastSeenUtc,
    bool IsMissing,
    bool IsPinned,
    string LastAccessedUtc,
    IReadOnlyList<string> Tags);
