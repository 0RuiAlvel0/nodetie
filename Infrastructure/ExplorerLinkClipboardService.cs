using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NodeTie.Infrastructure.Context;
using NodeTie.Infrastructure.Explorer;
using NodeTie.Infrastructure.Hotkeys;
using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Resolution;

namespace NodeTie.Infrastructure;

public sealed class ExplorerLinkClipboardService
{
    private readonly CopySourcePathResolver _copySourcePathResolver;
    private readonly HotkeySettingsService _hotkeySettingsService;
    private readonly IFileIdentityService _fileIdentityService;

    public ExplorerLinkClipboardService(
        CopySourcePathResolver copySourcePathResolver,
        HotkeySettingsService hotkeySettingsService,
        IFileIdentityService fileIdentityService)
    {
        _copySourcePathResolver = copySourcePathResolver;
        _hotkeySettingsService = hotkeySettingsService;
        _fileIdentityService = fileIdentityService;
    }

    public bool TryCopySelectedExplorerItemsToClipboard(out int copiedCount, out string message)
    {
        copiedCount = 0;
        message = string.Empty;

        if (!_copySourcePathResolver.TryResolvePathsForCopy(out IReadOnlyList<string> paths, out string source, out message)
            || paths.Count == 0)
        {
            return false;
        }

        CopyLinkTarget copyTarget = _hotkeySettingsService.LoadCopyTargetOrDefault();
        List<(string Path, string Uri)> linkItems = paths
            .Select(path => (Path: path, Uri: WinLinkUriCodec.EncodePath(path, TryBuildStableId(path))))
            .ToList();
        List<string> uriLinks = linkItems.Select(item => item.Uri).ToList();

        if (copyTarget == CopyLinkTarget.Obsidian)
        {
            List<string> markdownLinks = linkItems
                .Select(item => WinLinkUriCodec.EncodeMarkdownLink(item.Path, item.Uri))
                .ToList();
            Clipboard.SetText(string.Join(Environment.NewLine, markdownLinks));
        }
        else
        {
            ClipboardLinkContent clipboardContent = ClipboardLinkContentBuilder.Build(linkItems, copyTarget);

            DataObject clipboardPayload = new();
            if (!string.IsNullOrWhiteSpace(clipboardContent.PlainText))
            {
                clipboardPayload.SetText(clipboardContent.PlainText, TextDataFormat.UnicodeText);
            }

            if (!string.IsNullOrWhiteSpace(clipboardContent.HtmlText))
            {
                string clipboardHtml = HtmlClipboardFormatter.BuildClipboardHtml(clipboardContent.HtmlText);
                clipboardPayload.SetData(DataFormats.Html, clipboardHtml);
            }

            Clipboard.SetDataObject(clipboardPayload, true);
        }

        copiedCount = uriLinks.Count;
        message = copiedCount == 1
            ? $"Copied 1 file link from {source} ({copyTarget})."
            : $"Copied {copiedCount} file links from {source} ({copyTarget}).";
        return true;
    }

    public void CopySelectedExplorerItemsToClipboard()
    {
        TryCopySelectedExplorerItemsToClipboard(out _, out _);
    }

    private string? TryBuildStableId(string path)
    {
        return _fileIdentityService.TryGetIdentity(path, out FileIdentity identity)
            ? identity.ToStableId()
            : null;
    }
}
