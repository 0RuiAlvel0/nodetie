using System;
using System.IO;
using System.Text.RegularExpressions;
using NodeTie.Infrastructure;

namespace NodeTie.Infrastructure.Linking;

public sealed class ClipboardLinkParser
{
    private static readonly Regex WinLinkUriRegex = new(
        "winlink://[^\\s\\]\\)>\"]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public bool TryParseTargetPath(string clipboardText, out string path)
    {
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return false;
        }

        string trimmed = clipboardText.Trim();
        if (TryExtractWinLinkUri(trimmed, out string link))
        {
            path = WinLinkUriCodec.DecodeLink(link);
            return true;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)))
        {
            path = string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
                ? uri.LocalPath
                : uri.ToString();
            return true;
        }

        if (Path.IsPathRooted(trimmed))
        {
            path = trimmed;
            return true;
        }

        return false;
    }

    private static bool TryExtractWinLinkUri(string text, out string link)
    {
        link = string.Empty;
        Match match = WinLinkUriRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        link = match.Value;
        return true;
    }
}
