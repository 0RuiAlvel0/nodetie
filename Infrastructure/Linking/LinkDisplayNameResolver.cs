using System;
using System.IO;

namespace NodeTie.Infrastructure.Linking;

internal static class LinkDisplayNameResolver
{
    internal static string Resolve(string path)
    {
        if (TryResolveWebDisplayName(path, out string webDisplayName))
        {
            return webDisplayName;
        }

        string name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static bool TryResolveWebDisplayName(string path, out string displayName)
    {
        displayName = string.Empty;

        if (!Uri.TryCreate(path, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string host = uri.Host;
        string lastSegment = uri.Segments.Length > 0 ? uri.Segments[^1].Trim('/') : string.Empty;
        displayName = string.IsNullOrWhiteSpace(lastSegment) ? host : $"{host}/{lastSegment}";
        return !string.IsNullOrWhiteSpace(displayName);
    }
}