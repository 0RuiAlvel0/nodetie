using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NodeTie.Infrastructure;

public static partial class NodeTieVersionResolver
{
    private const string UnknownVersion = "unknown";

    public static string GetDisplayVersion()
    {
        if (TryGetAssemblyVersion(out string version))
        {
            return version;
        }

        if (TryGetLatestInstallerVersion(AppContext.BaseDirectory, out version))
        {
            return version;
        }

        return UnknownVersion;
    }

    internal static bool TryGetAssemblyVersion(out string version)
    {
        version = UnknownVersion;

        string? productVersion = FileVersionInfo.GetVersionInfo(Application.ExecutablePath).ProductVersion;
        if (TryNormalizeVersion(productVersion, out version))
        {
            return true;
        }

        Version? assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version;
        if (assemblyVersion is not null && assemblyVersion != new Version(1, 0, 0, 0))
        {
            version = NormalizeVersion(assemblyVersion);
            return true;
        }

        return false;
    }

    internal static bool TryGetLatestInstallerVersion(string startDirectory, out string version)
    {
        version = UnknownVersion;

        string? installerRoot = FindInstallerArtifactsDirectory(startDirectory);
        if (string.IsNullOrWhiteSpace(installerRoot) || !Directory.Exists(installerRoot))
        {
            return false;
        }

        Version? bestVersion = null;
        string? bestVersionText = null;

        foreach (string directory in Directory.EnumerateDirectories(installerRoot))
        {
            string candidate = Path.GetFileName(directory);
            if (!TryNormalizeVersion(candidate, out string normalizedCandidate, out Version? parsedCandidate))
            {
                continue;
            }

            if (bestVersion is null || parsedCandidate > bestVersion)
            {
                bestVersion = parsedCandidate;
                bestVersionText = normalizedCandidate;
            }
        }

        if (bestVersionText is null)
        {
            return false;
        }

        version = bestVersionText;
        return true;
    }

    internal static bool TryNormalizeVersion(string? candidate, out string normalizedVersion)
    {
        return TryNormalizeVersion(candidate, out normalizedVersion, out _);
    }

    internal static bool TryNormalizeVersion(string? candidate, out string normalizedVersion, out Version? parsedVersion)
    {
        normalizedVersion = UnknownVersion;
        parsedVersion = null;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        Match match = VersionPattern().Match(candidate.Trim());
        if (!match.Success)
        {
            return false;
        }

        string numericPortion = match.Groups[1].Value;
        if (!Version.TryParse(numericPortion, out parsedVersion))
        {
            return false;
        }

        normalizedVersion = NormalizeVersion(parsedVersion);
        return true;
    }

    private static string NormalizeVersion(Version version)
    {
        int[] parts = [version.Major, version.Minor, Math.Max(version.Build, 0), version.Revision];
        int partCount = parts[3] > 0 ? 4 : 3;

        return string.Join('.', parts.Take(partCount));
    }

    private static string? FindInstallerArtifactsDirectory(string startDirectory)
    {
        DirectoryInfo? current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "artifacts", "installer");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    [GeneratedRegex(@"^(\d+\.\d+\.\d+(?:\.\d+)?)(?:[-+].*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}