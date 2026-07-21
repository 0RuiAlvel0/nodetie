using System.Collections.Generic;

namespace NodeTie.Infrastructure.Persistence;

public sealed record BookmarkedFileRecord(
    long FileId,
    string StableId,
    string CurrentPath,
    string DisplayName,
    bool IsMissing,
    string CreatedUtc,
    string UpdatedUtc,
    bool IsPinned,
    string LastAccessedUtc,
    IReadOnlyList<string> Tags);
