using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NodeTie.Infrastructure.Context.Office;

public sealed class OfficePathResolver
{
    private readonly IReadOnlyList<string> _candidateRoots;

    public OfficePathResolver()
        : this(null)
    {
    }

    public OfficePathResolver(IEnumerable<string>? candidateRoots)
    {
        _candidateRoots = (candidateRoots ?? DiscoverCandidateRoots()).ToList();
    }

    public bool TryResolvePreferredPath(string officePath, out string preferredPath)
    {
        preferredPath = officePath;

        if (string.IsNullOrWhiteSpace(officePath))
        {
            return false;
        }

        if (File.Exists(officePath) || Directory.Exists(officePath))
        {
            preferredPath = officePath;
            return true;
        }

        if (!TryBuildLocalCandidatePath(officePath, out string candidatePath))
        {
            return false;
        }

        if (File.Exists(candidatePath) || Directory.Exists(candidatePath))
        {
            preferredPath = candidatePath;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> DiscoverCandidateRoots()
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

        void AddIfExists(string? root)
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                roots.Add(root);
            }
        }

        AddIfExists(Environment.GetEnvironmentVariable("OneDrive"));
        AddIfExists(Environment.GetEnvironmentVariable("OneDriveConsumer"));
        AddIfExists(Environment.GetEnvironmentVariable("OneDriveCommercial"));

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
        {
            foreach (string directory in Directory.EnumerateDirectories(userProfile, "OneDrive*", SearchOption.TopDirectoryOnly))
            {
                roots.Add(directory);
            }
        }

        return roots;
    }

    private bool TryBuildLocalCandidatePath(string officePath, out string candidatePath)
    {
        candidatePath = string.Empty;

        if (!Uri.TryCreate(officePath, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !uri.Host.Contains("docs.live.net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        List<string> segments = uri.Segments
            .Select(segment => Uri.UnescapeDataString(segment).Trim('/'))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count <= 1)
        {
            return false;
        }

        string relativePath = Path.Combine(segments.Skip(1).ToArray());
        foreach (string root in _candidateRoots)
        {
            string combinedPath = Path.Combine(root, relativePath);
            if (File.Exists(combinedPath) || Directory.Exists(combinedPath))
            {
                candidatePath = combinedPath;
                return true;
            }
        }

        return false;
    }
}