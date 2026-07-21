using System;
using System.Collections.Generic;

namespace NodeTie.Infrastructure.Context.Browser;

internal static class BrowserUrlResolver
{
    internal static bool TrySelectUrl(IEnumerable<string> candidates, out string url)
    {
        foreach (string candidate in candidates)
        {
            if (TryNormalizeUrl(candidate, out url))
            {
                return true;
            }
        }

        url = string.Empty;
        return false;
    }

    private static bool TryNormalizeUrl(string candidate, out string url)
    {
        url = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string trimmed = candidate.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        url = string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
            ? uri.LocalPath
            : uri.ToString();
        return true;
    }
}