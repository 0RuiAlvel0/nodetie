namespace NodeTie.Infrastructure.Resolution;

public sealed record FileResolutionResult(bool Found, string? ResolvedPath, bool UsedAliasPath)
{
    public static FileResolutionResult NotFound { get; } = new(false, null, false);

    public static FileResolutionResult FoundAt(string path, bool usedAliasPath)
    {
        return new FileResolutionResult(true, path, usedAliasPath);
    }
}
