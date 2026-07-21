using System;
using System.IO;
using System.Net;

namespace NodeTie.Infrastructure;

public static class WinLinkUriCodec
{
    private const string WinLinkScheme = "winlink://";

    public static string EncodePath(string path)
    {
        return EncodePath(path, stableId: null);
    }

    public static string EncodePath(string path, string? stableId)
    {
        string cleanPath = path.Replace("\\", "/");
        string encodedPath = Uri.EscapeDataString(cleanPath);
        if (string.IsNullOrWhiteSpace(stableId))
        {
            return $"{WinLinkScheme}/{encodedPath}";
        }

        string encodedStableId = Uri.EscapeDataString(stableId);
        return $"{WinLinkScheme}/{encodedPath}?sid={encodedStableId}";
    }

    public static string EncodeMarkdownLink(string path)
    {
        return EncodeMarkdownLink(path, EncodePath(path));
    }

    public static string EncodeMarkdownLink(string path, string uri)
    {
        string label = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = path;
        }

        string escapedLabel = EscapeMarkdownLabel(label);
        return $"[{escapedLabel}]({uri})";
    }

    public static string EncodeHtmlLink(string path)
    {
        return EncodeHtmlLink(path, EncodePath(path));
    }

    public static string EncodeHtmlLink(string path, string uri)
    {
        string label = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = path;
        }

        string escapedLabel = WebUtility.HtmlEncode(label);
        return $"<a href=\"{uri}\">{escapedLabel}</a>";
    }

    public static string DecodeLink(string clickedLink)
    {
        return TryDecodeLink(clickedLink, out string path, out _) ? path : string.Empty;
    }

    public static bool TryDecodeLink(string clickedLink, out string path, out string? stableId)
    {
        path = string.Empty;
        stableId = null;

        if (string.IsNullOrWhiteSpace(clickedLink)
            || !clickedLink.StartsWith(WinLinkScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(clickedLink, UriKind.Absolute, out Uri? uri))
        {
            string payload = clickedLink[WinLinkScheme.Length..];
            payload = Uri.UnescapeDataString(payload);
            payload = payload.TrimStart('/');
            path = payload.Replace('/', '\\');
            return !string.IsNullOrWhiteSpace(path);
        }

        string decodedPath = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', '\\');
        if (string.IsNullOrWhiteSpace(decodedPath))
        {
            return false;
        }

        if (decodedPath.StartsWith("https:\\\\", StringComparison.OrdinalIgnoreCase)
            || decodedPath.StartsWith("http:\\\\", StringComparison.OrdinalIgnoreCase))
        {
            decodedPath = decodedPath.Replace('\\', '/');
        }

        if (!string.IsNullOrWhiteSpace(uri.Query))
        {
            string query = uri.Query.TrimStart('?');
            foreach (string segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] keyValue = segment.Split('=', 2);
                if (keyValue.Length != 2)
                {
                    continue;
                }

                if (!string.Equals(keyValue[0], "sid", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string decodedStableId = Uri.UnescapeDataString(keyValue[1]);
                if (!string.IsNullOrWhiteSpace(decodedStableId))
                {
                    stableId = decodedStableId;
                }

                break;
            }
        }

        path = decodedPath;
        return true;
    }

    private static string EscapeMarkdownLabel(string label)
    {
        return label
            .Replace("\\", "\\\\")
            .Replace("]", "\\]");
    }
}
