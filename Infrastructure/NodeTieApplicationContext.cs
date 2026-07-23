using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NodeTie.Infrastructure.Hotkeys;
using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;

namespace NodeTie.Infrastructure;

public sealed class NodeTieApplicationContext : ApplicationContext
{
    private const int OpenPanelHotkeyId = 7001;
    private const int CopySelectionHotkeyId = 7002;

    private readonly NotifyIcon _notifyIcon;
    private readonly NodeTieMessageWindow _messageWindow;
    private readonly ExplorerLinkClipboardService _clipboardService;
    private readonly SelectedFileService _selectedFileService;
    private readonly LinkedFileQueryService _linkedFileQueryService;
    private readonly LinkedFileOpenService _linkedFileOpenService;
    private readonly LinkCompositionService _linkCompositionService;
    private readonly LinkRemovalService _linkRemovalService;
    private readonly BookmarkService _bookmarkService;
    private readonly HotkeySettingsService _hotkeySettingsService;
    private readonly WindowsStartupRegistrationService _startupRegistrationService;
    private readonly GlobalHotkeyManager _openPanelHotkeyManager;
    private readonly GlobalHotkeyManager _copySelectionHotkeyManager;
    private LinkedFilesPanelForm? _panel;
    private BookmarksWindowForm? _bookmarksWindow;
    private FileRecord? _currentSelectedFile;

    public NodeTieApplicationContext(
        ExplorerLinkClipboardService clipboardService,
        SelectedFileService selectedFileService,
        LinkedFileQueryService linkedFileQueryService,
        LinkedFileOpenService linkedFileOpenService,
        LinkCompositionService linkCompositionService,
        LinkRemovalService linkRemovalService,
        BookmarkService bookmarkService,
        HotkeySettingsService hotkeySettingsService,
        WindowsStartupRegistrationService startupRegistrationService)
    {
        _clipboardService = clipboardService;
        _selectedFileService = selectedFileService;
        _linkedFileQueryService = linkedFileQueryService;
        _linkedFileOpenService = linkedFileOpenService;
        _linkCompositionService = linkCompositionService;
        _linkRemovalService = linkRemovalService;
        _bookmarkService = bookmarkService;
        _hotkeySettingsService = hotkeySettingsService;
        _startupRegistrationService = startupRegistrationService;
        _messageWindow = new NodeTieMessageWindow();
        _messageWindow.HotKeyPressed += OnHotKeyPressed;
        _openPanelHotkeyManager = new GlobalHotkeyManager(_messageWindow, OpenPanelHotkeyId);
        _copySelectionHotkeyManager = new GlobalHotkeyManager(_messageWindow, CopySelectionHotkeyId);
        _notifyIcon = CreateNotifyIcon();

        // Apply persisted keyboard and startup preferences as soon as the tray app starts.
        (HotkeyBinding openPanelBinding, HotkeyBinding copySelectionBinding) = _hotkeySettingsService.LoadAllOrDefault();
        ApplyHotkeyBindings(openPanelBinding, copySelectionBinding, allowFallback: true, showErrorMessage: true);

        bool startupDefault = _startupRegistrationService.IsEnabledForCurrentUser();
        bool runAtLogin = _hotkeySettingsService.LoadRunAtLoginOrDefault(startupDefault);
        ApplyStartupPreference(runAtLogin, showErrorMessage: false);
    }

    private NotifyIcon CreateNotifyIcon()
    {
        ContextMenuStrip menu = new();

        ToolStripMenuItem panelMenuItem = new("Open Link Panel")
        {
            ToolTipText = "Open linked files for the current Explorer selection"
        };
        panelMenuItem.Click += (_, _) => ShowLinkedFilesPanel();

        ToolStripMenuItem copyLinkMenuItem = new("Copy Explorer Selection")
        {
            ToolTipText = "Copies links using your configured target app format"
        };
        copyLinkMenuItem.Click += (_, _) => CopyExplorerSelectionLinksWithFeedback(showMessageBoxOnError: true);

        ToolStripMenuItem bookmarksMenuItem = new("Open Bookmarks")
        {
            ToolTipText = "Open pinned, recent, and all bookmarks"
        };
        bookmarksMenuItem.Click += (_, _) => ShowBookmarksWindow();

        ToolStripMenuItem bookmarkCurrentMenuItem = new("Bookmark Current Item")
        {
            ToolTipText = "Bookmark the current active file or URL"
        };
        bookmarkCurrentMenuItem.Click += (_, _) => BookmarkCurrentItem(showMessageBoxOnError: true);

        ToolStripMenuItem settingsMenuItem = new("Settings...");
        settingsMenuItem.Click += (_, _) => ShowSettingsDialog();

        ToolStripMenuItem aboutMenuItem = new("About");
        aboutMenuItem.Click += (_, _) => ShowAboutDialog();

        ToolStripMenuItem exitMenuItem = new("Exit");
        exitMenuItem.Click += (_, _) => ExitThread();

        menu.Items.Add(panelMenuItem);
        menu.Items.Add(copyLinkMenuItem);
        menu.Items.Add(bookmarksMenuItem);
        menu.Items.Add(bookmarkCurrentMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsMenuItem);
        menu.Items.Add(aboutMenuItem);
        menu.Items.Add(exitMenuItem);

        Icon trayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        return new NotifyIcon
        {
            Text = "NodeTie",
            Icon = trayIcon,
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    protected override void ExitThreadCore()
    {
        _messageWindow.HotKeyPressed -= OnHotKeyPressed;
        if (_panel is not null)
        {
            _panel.LinkedFilesActivated -= OnLinkedFilesActivated;
            _panel.Dispose();
            _panel = null;
        }

        if (_bookmarksWindow is not null)
        {
            _bookmarksWindow.QueryRequested -= OnBookmarksQueryRequested;
            _bookmarksWindow.OpenRequested -= OnBookmarksOpenRequested;
            _bookmarksWindow.TogglePinRequested -= OnBookmarksTogglePinRequested;
            _bookmarksWindow.RemoveBookmarksRequested -= OnBookmarksRemoveRequested;
            _bookmarksWindow.AddTagRequested -= OnBookmarksAddTagRequested;
            _bookmarksWindow.RemoveTagRequested -= OnBookmarksRemoveTagRequested;
            _bookmarksWindow.BookmarkCurrentRequested -= OnBookmarksBookmarkCurrentRequested;
            _bookmarksWindow.Dispose();
            _bookmarksWindow = null;
        }

        _openPanelHotkeyManager.Dispose();
        _copySelectionHotkeyManager.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _messageWindow.Dispose();
        base.ExitThreadCore();
    }

    private void OnHotKeyPressed(object? sender, int hotkeyId)
    {
        if (hotkeyId == _openPanelHotkeyManager.HotkeyId)
        {
            ShowLinkedFilesPanel();
            return;
        }

        if (hotkeyId == _copySelectionHotkeyManager.HotkeyId)
        {
            CopyExplorerSelectionLinksWithFeedback(showMessageBoxOnError: false);
        }
    }

    private void ShowLinkedFilesPanel()
    {
        if (!_selectedFileService.TryGetOrCreateSelectedFile(out var selectedFile, out string errorMessage)
            || selectedFile is null)
        {
            MessageBox.Show(errorMessage, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _currentSelectedFile = selectedFile;
        var linkedFiles = _linkedFileQueryService.GetLinkedFiles(selectedFile.Id);

        // Ensure only one panel is active so repeated hotkey presses refresh focus cleanly.
        if (_panel is not null)
        {
            _panel.RefreshContents(selectedFile, linkedFiles);
            _panel.SetStatus($"{linkedFiles.Count} linked file(s)");
            _panel.Activate();
            _panel.BringToFront();
            return;
        }

        _panel = new LinkedFilesPanelForm(selectedFile, linkedFiles);
        _panel.LinkedFilesActivated += OnLinkedFilesActivated;
        _panel.CopyLinkRequested += OnCopyLinkRequested;
        _panel.PasteClipboardRequested += OnPasteClipboardRequested;
        _panel.LinkTextSubmitted += OnLinkTextSubmitted;
        _panel.GlobalSearchRequested += OnGlobalSearchRequested;
        _panel.LinkKnownFilesRequested += OnLinkKnownFilesRequested;
        _panel.RemoveSelectedLinksRequested += OnRemoveSelectedLinksRequested;
        _panel.BookmarkCurrentRequested += OnBookmarkCurrentRequested;
        _panel.FormClosed += (_, _) =>
        {
            if (_panel is null)
            {
                return;
            }

            _panel.LinkedFilesActivated -= OnLinkedFilesActivated;
            _panel.CopyLinkRequested -= OnCopyLinkRequested;
            _panel.PasteClipboardRequested -= OnPasteClipboardRequested;
            _panel.LinkTextSubmitted -= OnLinkTextSubmitted;
            _panel.GlobalSearchRequested -= OnGlobalSearchRequested;
            _panel.LinkKnownFilesRequested -= OnLinkKnownFilesRequested;
            _panel.RemoveSelectedLinksRequested -= OnRemoveSelectedLinksRequested;
            _panel.BookmarkCurrentRequested -= OnBookmarkCurrentRequested;
            _panel.Dispose();
            _panel = null;
        };
        _panel.Show();
        _panel.Activate();
    }

    private void OnCopyLinkRequested(object? sender, EventArgs e)
    {
        if (_currentSelectedFile is null)
        {
            return;
        }

        string uri = WinLinkUriCodec.EncodePath(_currentSelectedFile.CurrentPath, _currentSelectedFile.StableId);
        CopyLinkTarget copyTarget = _hotkeySettingsService.LoadCopyTargetOrDefault();
        ClipboardLinkContent clipboardContent = ClipboardLinkContentBuilder.Build(
            new[] { (_currentSelectedFile.CurrentPath, uri) },
            copyTarget);

        if (!string.IsNullOrWhiteSpace(clipboardContent.PlainText)
            && !string.IsNullOrWhiteSpace(clipboardContent.HtmlText))
        {
            DataObject clipboardPayload = new();
            clipboardPayload.SetText(clipboardContent.PlainText, TextDataFormat.UnicodeText);
            clipboardPayload.SetData(
                DataFormats.Html,
                HtmlClipboardFormatter.BuildClipboardHtml(clipboardContent.HtmlText));
            Clipboard.SetDataObject(clipboardPayload, true);
        }
        else if (!string.IsNullOrWhiteSpace(clipboardContent.PlainText))
        {
            Clipboard.SetText(clipboardContent.PlainText);
        }
        else if (!string.IsNullOrWhiteSpace(clipboardContent.HtmlText))
        {
            Clipboard.SetDataObject(
                new DataObject(DataFormats.Html, HtmlClipboardFormatter.BuildClipboardHtml(clipboardContent.HtmlText)),
                true);
        }

        _panel?.SetStatus($"Copied link for {_currentSelectedFile.DisplayName}");
    }

    private async void OnBookmarkCurrentRequested(object? sender, EventArgs e)
    {
        if (_panel is null)
        {
            BookmarkCurrentItem(showMessageBoxOnError: true);
            return;
        }

        await BookmarkCurrentFromOverlayWindowAsync(_panel);
    }

    private void OnLinkTextSubmitted(object? sender, string clipboardText)
    {
        if (_currentSelectedFile is null)
        {
            return;
        }

        bool linked = _linkCompositionService.TryLinkManyFromClipboard(
            _currentSelectedFile.Id,
            clipboardText,
            out int createdCount,
            out int skippedCount,
            out string message);
        if (!linked)
        {
            MessageBox.Show(message, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshPanelContents();
        if (skippedCount == 0)
        {
            _panel?.SetStatus(createdCount == 1 ? "Linked 1 file." : $"Linked {createdCount} files.");
            return;
        }

        _panel?.SetStatus($"Linked {createdCount} file(s); skipped {skippedCount}.");
    }

    private void OnGlobalSearchRequested(object? sender, string searchText)
    {
        if (_currentSelectedFile is null || _panel is null)
        {
            return;
        }

        IReadOnlyList<LinkDisplayItem> results = _linkedFileQueryService.SearchKnownFilesForLinking(_currentSelectedFile.Id, searchText);
        string status = results.Count == 0
            ? "No matching known files."
            : $"Found {results.Count} matching file(s). Select and press Enter or click Link selected.";
        _panel.ShowGlobalSearchResults(results, status);
    }

    private void OnLinkKnownFilesRequested(object? sender, IReadOnlyList<long> fileIds)
    {
        if (_currentSelectedFile is null)
        {
            return;
        }

        bool linked = _linkCompositionService.TryLinkKnownFiles(
            _currentSelectedFile.Id,
            fileIds,
            out int createdCount,
            out int skippedCount,
            out string message);
        if (!linked)
        {
            MessageBox.Show(message, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _panel?.ExitGlobalSearchMode();
        RefreshPanelContents();
        _panel?.SetStatus(skippedCount == 0
            ? (createdCount == 1 ? "Linked 1 file from search." : $"Linked {createdCount} files from search.")
            : $"Linked {createdCount} file(s) from search; skipped {skippedCount}.");
    }

    private void OnPasteClipboardRequested(object? sender, EventArgs e)
    {
        if (_currentSelectedFile is null)
        {
            return;
        }

        if (!Clipboard.ContainsText())
        {
            MessageBox.Show("Clipboard does not contain text links or file paths.", "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string clipboardText = Clipboard.GetText();
        bool linked = _linkCompositionService.TryLinkManyFromClipboard(
            _currentSelectedFile.Id,
            clipboardText,
            out int createdCount,
            out int skippedCount,
            out string message);
        if (!linked)
        {
            MessageBox.Show(message, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshPanelContents();
        if (skippedCount == 0)
        {
            _panel?.SetStatus(createdCount == 1 ? "Linked 1 file from clipboard." : $"Linked {createdCount} files from clipboard.");
            return;
        }

        _panel?.SetStatus($"Linked {createdCount} file(s) from clipboard; skipped {skippedCount}.");
    }

    private void OnRemoveSelectedLinksRequested(object? sender, IReadOnlyList<long> linkedFileIds)
    {
        try
        {
            if (_currentSelectedFile is null)
            {
                return;
            }

            int selectionCount = linkedFileIds.Distinct().Count();
            if (selectionCount == 0)
            {
                return;
            }

            string confirmationText = selectionCount == 1
                ? "Do you want to remove this linked file?"
                : $"Do you want to remove these {selectionCount} files?";
            DialogResult confirmation = MessageBox.Show(
                confirmationText,
                "Confirm unlink",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            int removedCount = 0;
            foreach (long linkedFileId in linkedFileIds.Distinct())
            {
                if (_linkRemovalService.TryRemoveLink(_currentSelectedFile.Id, linkedFileId))
                {
                    removedCount++;
                }
            }

            if (removedCount == 0)
            {
                MessageBox.Show("The selected links could not be removed.", "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshPanelContents();
            _panel?.SetStatus(removedCount == 1 ? "Removed 1 link." : $"Removed {removedCount} links.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"NodeTie could not remove the selected links.\n\nError: {ex.Message}", "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshPanelContents()
    {
        if (_panel is null || _currentSelectedFile is null)
        {
            return;
        }

        // Keep the panel anchored to the file it was opened for; re-resolving here can drift to
        // another foreground source while the panel is active and hide the links we just created.
        var linkedFiles = _linkedFileQueryService.GetLinkedFiles(_currentSelectedFile.Id);
        _panel.RefreshContents(_currentSelectedFile, linkedFiles);
    }

    private async void OnLinkedFilesActivated(object? sender, IReadOnlyList<long> fileIds)
    {
        if (fileIds.Count == 0)
        {
            return;
        }

        (int openedCount, List<string> failures) = await Task.Run(() =>
        {
            int opened = 0;
            List<string> failures = [];
            foreach (long fileId in fileIds.Distinct())
            {
                if (_linkedFileOpenService.TryOpenFile(fileId, out string message))
                {
                    opened++;
                    continue;
                }

                failures.Add(message);
            }

            return (opened, failures);
        });

        if (openedCount > 0)
        {
            _panel?.SetStatus(openedCount == 1 ? "Opened 1 linked file." : $"Opened {openedCount} linked files.");
        }

        if (failures.Count == 0)
        {
            return;
        }

        string failureMessage = string.Join(Environment.NewLine, failures.Distinct());
        MessageBox.Show(failureMessage, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ShowSettingsDialog()
    {
        HotkeyBinding openPanelCurrent = _openPanelHotkeyManager.CurrentBinding ?? _hotkeySettingsService.LoadOpenPanelOrDefault();
        HotkeyBinding copySelectionCurrent = _copySelectionHotkeyManager.CurrentBinding ?? _hotkeySettingsService.LoadCopySelectionOrDefault();
        CopyLinkTarget copyTargetCurrent = _hotkeySettingsService.LoadCopyTargetOrDefault();
        bool startupDefault = _startupRegistrationService.IsEnabledForCurrentUser();
        bool runAtLoginCurrent = _hotkeySettingsService.LoadRunAtLoginOrDefault(startupDefault);
        using HotkeySettingsForm settingsForm = new(openPanelCurrent, copySelectionCurrent, copyTargetCurrent, runAtLoginCurrent);
        if (settingsForm.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        if (!settingsForm.TryGetSelectedSettings(out HotkeyBinding openPanelSelected, out HotkeyBinding copySelectionSelected, out CopyLinkTarget copyTargetSelected, out bool runAtLoginSelected))
        {
            return;
        }

        if (!ApplyHotkeyBindings(openPanelSelected, copySelectionSelected, allowFallback: false, showErrorMessage: true))
        {
            return;
        }

        _hotkeySettingsService.Save(openPanelSelected, copySelectionSelected, copyTargetSelected, runAtLoginSelected);
        ApplyStartupPreference(runAtLoginSelected, showErrorMessage: true);
    }

    private void ApplyStartupPreference(bool runAtLogin, bool showErrorMessage)
    {
        bool updated = _startupRegistrationService.TrySetEnabled(Application.ExecutablePath, runAtLogin, out string startupUpdateError);
        if (updated)
        {
            return;
        }

        StartupDiagnostics.Error($"Startup registration update failed: {startupUpdateError}");
        if (!showErrorMessage)
        {
            return;
        }

        MessageBox.Show(
            $"NodeTie could not update Windows Startup Apps.\n\n{startupUpdateError}",
            "NodeTie",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void ShowAboutDialog()
    {
        string version = NodeTieVersionResolver.GetDisplayVersion();
        MessageBox.Show(
            $"NodeTie\nVersion {version}",
            "About NodeTie",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private bool ApplyHotkeyBindings(HotkeyBinding openPanelBinding, HotkeyBinding copySelectionBinding, bool allowFallback, bool showErrorMessage)
    {
        if (!allowFallback)
        {
            bool openPanelRegistered = _openPanelHotkeyManager.Register(openPanelBinding);
            if (!openPanelRegistered)
            {
                StartupDiagnostics.Error($"Open-panel hotkey registration failed for '{openPanelBinding}'.");
                if (showErrorMessage)
                {
                    MessageBox.Show(
                        $"Could not register open-panel hotkey '{openPanelBinding}'. It may already be used by another app.",
                        "Hotkey registration failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return false;
            }

            bool copySelectionRegistered = _copySelectionHotkeyManager.Register(copySelectionBinding);
            if (copySelectionRegistered)
            {
                return true;
            }

            StartupDiagnostics.Error($"Copy-selection hotkey registration failed for '{copySelectionBinding}'. Open-panel hotkey remains active.");
            if (showErrorMessage)
            {
                MessageBox.Show(
                    $"Could not register copy-selection hotkey '{copySelectionBinding}'. It may already be used by another app.",
                    "Hotkey registration failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return false;
        }

        HotkeyRegistrationPlanResult result = HotkeyRegistrationPlanner.Resolve(
            openPanelBinding,
            HotkeySettingsService.GetOpenPanelFallbackCandidates(openPanelBinding),
            copySelectionBinding,
            HotkeySettingsService.GetCopySelectionFallbackCandidates(copySelectionBinding),
            _openPanelHotkeyManager.Register,
            _openPanelHotkeyManager.Unregister,
            _copySelectionHotkeyManager.Register);

        if (result.Success)
        {
            if (result.UsedOpenPanelFallback || result.UsedCopySelectionFallback)
            {
                string appliedOpenPanel = result.RegisteredOpenPanelBinding?.ToString() ?? openPanelBinding.ToString();
                string appliedCopySelection = result.RegisteredCopySelectionBinding?.ToString() ?? copySelectionBinding.ToString();
                string message = $"Hotkey conflict detected. Using '{appliedOpenPanel}' for open panel and '{appliedCopySelection}' for copy selection.";
                StartupDiagnostics.Info(message);
                ShowTrayNotification(message, ToolTipIcon.Warning);
            }

            return true;
        }

        StartupDiagnostics.Error($"Hotkey registration failed for preferred bindings '{openPanelBinding}' and '{copySelectionBinding}', and all fallback candidates were unavailable.");
        if (showErrorMessage)
        {
            MessageBox.Show(
                $"NodeTie could not register the configured hotkeys or any fallback alternatives. Open Settings from the tray icon to choose different shortcuts.",
                "Hotkey registration failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        return false;
    }

    private void CopyExplorerSelectionLinksWithFeedback(bool showMessageBoxOnError)
    {
        bool copied = _clipboardService.TryCopySelectedExplorerItemsToClipboard(out int copiedCount, out string message);
        if (copied)
        {
            _panel?.SetStatus(message);
            ShowTrayNotification(message, ToolTipIcon.Info);
            return;
        }

        if (showMessageBoxOnError)
        {
            MessageBox.Show(message, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        _panel?.SetStatus(message);
        ShowTrayNotification(message, ToolTipIcon.Warning);
    }

    private void ShowTrayNotification(string message, ToolTipIcon icon)
    {
        _notifyIcon.ShowBalloonTip(2000, "NodeTie", message, icon);
    }

    private void BookmarkCurrentItem(bool showMessageBoxOnError)
    {
        if (!_selectedFileService.TryGetOrCreateSelectedFile(out FileRecord? selectedFile, out string errorMessage)
            || selectedFile is null)
        {
            if (showMessageBoxOnError)
            {
                MessageBox.Show(errorMessage, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (_bookmarksWindow is not null)
            {
                _bookmarksWindow.ShowResults(
                    _bookmarkService.Query(_bookmarksWindow.SelectedView, _bookmarksWindow.SearchTerm, _bookmarksWindow.TagFilter),
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? "No active file context was found."
                        : errorMessage);
            }

            _panel?.SetStatus(string.IsNullOrWhiteSpace(errorMessage)
                ? "No active file context was found."
                : errorMessage);

            return;
        }

        bool changed = _bookmarkService.EnsureBookmarked(selectedFile.Id, source: "manual");
        string message = changed
            ? $"Bookmarked {selectedFile.DisplayName}."
            : $"Updated bookmark timestamp for {selectedFile.DisplayName}.";

        _panel?.SetStatus(message);
        _bookmarksWindow?.BringToFront();
        ShowTrayNotification(message, ToolTipIcon.Info);
        if (_bookmarksWindow is not null)
        {
            RefreshBookmarksWindow();
        }
    }

    private async Task BookmarkCurrentFromOverlayWindowAsync(Form overlayWindow)
    {
        bool wasVisible = overlayWindow.Visible;
        if (wasVisible)
        {
            overlayWindow.Hide();
        }

        // Give focus back to the previously active app before reading context.
        await Task.Delay(180);

        BookmarkCurrentItem(showMessageBoxOnError: true);

        if (wasVisible)
        {
            overlayWindow.Show();
            overlayWindow.Activate();
            overlayWindow.BringToFront();
        }
    }

    private void ShowBookmarksWindow()
    {
        if (_bookmarksWindow is not null)
        {
            _bookmarksWindow.Activate();
            _bookmarksWindow.BringToFront();
            return;
        }

        _bookmarksWindow = new BookmarksWindowForm();
        _bookmarksWindow.QueryRequested += OnBookmarksQueryRequested;
        _bookmarksWindow.OpenRequested += OnBookmarksOpenRequested;
        _bookmarksWindow.TogglePinRequested += OnBookmarksTogglePinRequested;
        _bookmarksWindow.RemoveBookmarksRequested += OnBookmarksRemoveRequested;
        _bookmarksWindow.AddTagRequested += OnBookmarksAddTagRequested;
        _bookmarksWindow.RemoveTagRequested += OnBookmarksRemoveTagRequested;
        _bookmarksWindow.BookmarkCurrentRequested += OnBookmarksBookmarkCurrentRequested;
        _bookmarksWindow.FormClosed += (_, _) =>
        {
            if (_bookmarksWindow is null)
            {
                return;
            }

            _bookmarksWindow.QueryRequested -= OnBookmarksQueryRequested;
            _bookmarksWindow.OpenRequested -= OnBookmarksOpenRequested;
            _bookmarksWindow.TogglePinRequested -= OnBookmarksTogglePinRequested;
            _bookmarksWindow.RemoveBookmarksRequested -= OnBookmarksRemoveRequested;
            _bookmarksWindow.AddTagRequested -= OnBookmarksAddTagRequested;
            _bookmarksWindow.RemoveTagRequested -= OnBookmarksRemoveTagRequested;
            _bookmarksWindow.BookmarkCurrentRequested -= OnBookmarksBookmarkCurrentRequested;
            _bookmarksWindow.Dispose();
            _bookmarksWindow = null;
        };
        _bookmarksWindow.Show();
        _bookmarksWindow.Activate();
    }

    private void RefreshBookmarksWindow()
    {
        if (_bookmarksWindow is null)
        {
            return;
        }

        IReadOnlyList<BookmarkedLinkDisplayItem> results = _bookmarkService.Query(
            _bookmarksWindow.SelectedView,
            _bookmarksWindow.SearchTerm,
            _bookmarksWindow.TagFilter);
        string status = results.Count == 0
            ? "No bookmarks match the current filters."
            : $"Loaded {results.Count} bookmark(s).";
        _bookmarksWindow.ShowResults(results, status);
    }

    private void OnBookmarksQueryRequested(object? sender, (BookmarkView View, string Search, string TagFilter) criteria)
    {
        RefreshBookmarksWindow();
    }

    private async void OnBookmarksOpenRequested(object? sender, IReadOnlyList<long> fileIds)
    {
        if (fileIds.Count == 0)
        {
            return;
        }

        (int openedCount, List<string> failures) = await Task.Run(() =>
        {
            int opened = 0;
            List<string> failures = [];
            foreach (long fileId in fileIds.Distinct())
            {
                if (_linkedFileOpenService.TryOpenFile(fileId, out string message))
                {
                    opened++;
                    continue;
                }

                failures.Add(message);
            }

            return (opened, failures);
        });

        if (openedCount > 0)
        {
            if (_bookmarksWindow is not null)
            {
                _bookmarksWindow.ShowResults(
                    _bookmarkService.Query(_bookmarksWindow.SelectedView, _bookmarksWindow.SearchTerm, _bookmarksWindow.TagFilter),
                    openedCount == 1 ? "Opened 1 bookmark." : $"Opened {openedCount} bookmarks.");
            }
        }

        if (failures.Count > 0)
        {
            string failureMessage = string.Join(Environment.NewLine, failures.Distinct());
            MessageBox.Show(failureMessage, "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnBookmarksTogglePinRequested(object? sender, IReadOnlyList<long> fileIds)
    {
        int changed = 0;
        foreach (long fileId in fileIds.Distinct())
        {
            if (_bookmarkService.TogglePinned(fileId))
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            RefreshBookmarksWindow();
        }
    }

    private void OnBookmarksRemoveRequested(object? sender, IReadOnlyList<long> fileIds)
    {
        try
        {
            if (fileIds.Count == 0)
            {
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                fileIds.Count == 1
                    ? "Delete this bookmark?"
                    : $"Delete these {fileIds.Count} bookmarks?",
                "Confirm bookmark deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            foreach (long fileId in fileIds.Distinct())
            {
                _bookmarkService.RemoveBookmark(fileId);
            }

            RefreshBookmarksWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"NodeTie could not delete the selected bookmarks.\n\nError: {ex.Message}", "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnBookmarksAddTagRequested(object? sender, (IReadOnlyList<long> FileIds, string Tag) request)
    {
        try
        {
            foreach (long fileId in request.FileIds.Distinct())
            {
                _bookmarkService.AddTag(fileId, request.Tag);
            }

            RefreshBookmarksWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"NodeTie could not add the tag.\n\nError: {ex.Message}", "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnBookmarksRemoveTagRequested(object? sender, (IReadOnlyList<long> FileIds, string Tag) request)
    {
        try
        {
            foreach (long fileId in request.FileIds.Distinct())
            {
                _bookmarkService.RemoveTag(fileId, request.Tag);
            }

            RefreshBookmarksWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"NodeTie could not remove the tag.\n\nError: {ex.Message}", "NodeTie", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnBookmarksBookmarkCurrentRequested(object? sender, EventArgs e)
    {
        if (_bookmarksWindow is null)
        {
            BookmarkCurrentItem(showMessageBoxOnError: true);
            RefreshBookmarksWindow();
            return;
        }

        await BookmarkCurrentFromOverlayWindowAsync(_bookmarksWindow);
        RefreshBookmarksWindow();
    }
}
