using System;
using System.Collections.Generic;
using System.Linq;
using NodeTie.Infrastructure.Hotkeys;

namespace NodeTie.Infrastructure.Linking;

internal sealed record ClipboardLinkContent(string? PlainText, string? HtmlText);

internal static class ClipboardLinkContentBuilder
{
    internal static ClipboardLinkContent Build(IReadOnlyList<(string Path, string Uri)> linkItems, CopyLinkTarget copyTarget)
    {
        return copyTarget switch
        {
            CopyLinkTarget.Obsidian => new ClipboardLinkContent(
                string.Join(Environment.NewLine, linkItems.Select(item => WinLinkUriCodec.EncodeMarkdownLink(item.Path, item.Uri))),
                null),
            CopyLinkTarget.OneNote => new ClipboardLinkContent(
                string.Join(Environment.NewLine, linkItems.Select(item => LinkDisplayNameResolver.Resolve(item.Path))),
                string.Join("<br/>", linkItems.Select(item => WinLinkUriCodec.EncodeHtmlLink(item.Path, item.Uri)))),
            _ => throw new ArgumentOutOfRangeException(nameof(copyTarget), copyTarget, null)
        };
    }
}