namespace NodeTie.Infrastructure.Persistence;

public sealed record FileRecord(
    long Id,
    string StableId,
    string CurrentPath,
    string DisplayName,
    bool IsMissing,
    string CreatedUtc,
    string UpdatedUtc
);
