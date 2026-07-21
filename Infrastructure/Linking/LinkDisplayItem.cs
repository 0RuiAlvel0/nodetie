namespace NodeTie.Infrastructure.Linking;

public sealed record LinkDisplayItem(
	long FileId,
	string Name,
	string Path,
	string FileExtension,
	string LastSeenUtc,
	bool IsMissing);
