using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Ui;

namespace NodeTie.Infrastructure.Linking;

public sealed class LinkedFilesPanelForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const int CsDropShadow = 0x00020000;

    private readonly Font _rowNameFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly Font _rowPathFont = new("Segoe UI", 8.5f, FontStyle.Regular);

    private readonly Label _currentNameLabel;
    private readonly Label _currentPathLabel;
    private readonly Label _currentStateLabel;
    private readonly Label _countLabel;
    private readonly Label _statusLabel;
    private readonly Label _searchModeHintLabel;
    private readonly Label _listHeadingLabel;
    private readonly TextBox _searchTextBox;
    private readonly CheckBox _searchAllCheckBox;
    private readonly TextBox _linkTextBox;
    private readonly ListBox _resultsListBox;
    private readonly Button _linkSelectedButton;
    private readonly Button _openSelectedButton;
    private readonly Button _removeSelectedButton;
    private readonly Button _bookmarkCurrentButton;

    private readonly List<LinkDisplayItem> _visibleLinkedFiles = [];
    private readonly ToolTip _helpToolTip = new();
    private IReadOnlyList<LinkDisplayItem> _allLinkedFiles;
    private FileRecord _selectedFile;

    public LinkedFilesPanelForm(FileRecord selectedFile, IReadOnlyList<LinkDisplayItem> linkedFiles)
    {
        _selectedFile = selectedFile;
        _allLinkedFiles = linkedFiles;

        Text = "NodeTie Links";
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = NodeTieTheme.DefaultFont;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = NodeTieTheme.Border;
        Padding = new Padding(1);

        _currentNameLabel = CreateSingleLineLabel(NodeTieTheme.TextPrimary, new Font("Segoe UI", 10.5f, FontStyle.Bold));
        _currentPathLabel = CreateSingleLineLabel(NodeTieTheme.TextSecondary, new Font("Segoe UI", 8.8f));
        _currentStateLabel = CreateSingleLineLabel(NodeTieTheme.Accent, new Font("Segoe UI", 8.6f));

        _countLabel = new Label
        {
            AutoSize = true,
            ForeColor = NodeTieTheme.TextSecondary,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(10, 3, 0, 0)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = NodeTieTheme.Muted,
            Padding = new Padding(4, 0, 0, 0)
        };

        _searchModeHintLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ForeColor = NodeTieTheme.Muted,
            Font = new Font("Segoe UI", 8.3f),
            Margin = new Padding(0, 2, 0, 2),
            Text = ""
        };

        _listHeadingLabel = new Label
        {
            Text = "LINKED FILES",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = NodeTieTheme.Muted,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Padding = new Padding(4, 0, 0, 0)
        };

        _searchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Search linked files",
            Margin = new Padding(0)
        };
        NodeTieTheme.ApplyTextBox(_searchTextBox);
        _searchTextBox.TextChanged += (_, _) => ApplySearchFilter();

        _searchAllCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Search all known files",
            ForeColor = NodeTieTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 4)
        };
        _helpToolTip.SetToolTip(
            _searchAllCheckBox,
            "When enabled, the list shows search results from all files NodeTie already knows.\nSelect results and press Enter or click Link selected.");
        _searchAllCheckBox.CheckedChanged += (_, _) => OnSearchModeChanged();

        _linkTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Paste paths or winlink:// links",
            Margin = new Padding(0)
        };
        NodeTieTheme.ApplyTextBox(_linkTextBox);

        _resultsListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(14, 20, 31),
            ForeColor = NodeTieTheme.TextPrimary,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            SelectionMode = SelectionMode.MultiExtended,
            HorizontalScrollbar = false
        };
        UpdateRowHeight();
        _resultsListBox.DrawItem += DrawLinkedFileRow;
        _resultsListBox.SelectedIndexChanged += (_, _) => UpdateSelectionActions();
        _resultsListBox.DoubleClick += (_, _) => TryRaiseLinkedFilesActivated();
        _resultsListBox.KeyDown += OnResultsKeyDown;

        _openSelectedButton = CreateActionButton("Open", NodeTieTheme.Accent, Color.Black);
        _openSelectedButton.Enabled = false;
        _openSelectedButton.Click += (_, _) => TryRaiseLinkedFilesActivated();

        _linkSelectedButton = CreateActionButton("Link selected", NodeTieTheme.Accent, Color.Black);
        _linkSelectedButton.Enabled = false;
        _linkSelectedButton.Click += (_, _) => TryRaiseLinkKnownFilesRequested();

        _removeSelectedButton = CreateActionButton("Remove", NodeTieTheme.Danger, Color.Black);
        _removeSelectedButton.Enabled = false;
        _removeSelectedButton.Click += (_, _) => TryRaiseRemoveSelectedLinksRequested();

        _bookmarkCurrentButton = CreateActionButton("Bookmark", NodeTieTheme.SurfaceAlt, NodeTieTheme.TextPrimary);
        _bookmarkCurrentButton.Click += (_, _) => BookmarkCurrentRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(BuildRootLayout());
        KeyDown += OnFormKeyDown;

        Shown += (_, _) =>
        {
            FitToCurrentScreen();
            UpdateCurrentFile();
            OnSearchModeChanged();
            _searchTextBox.Focus();
        };
    }

    public event EventHandler<IReadOnlyList<long>>? LinkedFilesActivated;
    public event EventHandler? CopyLinkRequested;
    public event EventHandler? PasteClipboardRequested;
    public event EventHandler<string>? LinkTextSubmitted;
    public event EventHandler<string>? GlobalSearchRequested;
    public event EventHandler<IReadOnlyList<long>>? LinkKnownFilesRequested;
    public event EventHandler<IReadOnlyList<long>>? RemoveSelectedLinksRequested;
    public event EventHandler? BookmarkCurrentRequested;

    public IReadOnlyList<long> SelectedLinkedFileIds => _resultsListBox.SelectedIndices
        .Cast<int>()
        .Where(index => index >= 0 && index < _visibleLinkedFiles.Count)
        .Select(index => _visibleLinkedFiles[index].FileId)
        .Distinct()
        .ToList();

    public FileRecord SelectedFile => _selectedFile;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            return parameters;
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UpdateRowHeight();
        Bounds = ConstrainBounds(e.SuggestedRectangle, Screen.FromRectangle(e.SuggestedRectangle).WorkingArea);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rowNameFont.Dispose();
            _rowPathFont.Dispose();
            _helpToolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    public void RefreshContents(FileRecord selectedFile, IReadOnlyList<LinkDisplayItem> linkedFiles)
    {
        _selectedFile = selectedFile;
        _allLinkedFiles = linkedFiles;
        UpdateCurrentFile();
        ApplySearchFilter();
    }

    public void SetStatus(string text)
    {
        _statusLabel.Text = text;
    }

    public void ExitGlobalSearchMode()
    {
        if (_searchAllCheckBox.Checked)
        {
            _searchAllCheckBox.Checked = false;
        }

        if (_searchTextBox.TextLength > 0)
        {
            _searchTextBox.Clear();
        }

        ApplySearchFilter();
    }

    public void ShowGlobalSearchResults(IReadOnlyList<LinkDisplayItem> searchResults, string statusText)
    {
        _visibleLinkedFiles.Clear();
        _visibleLinkedFiles.AddRange(searchResults);

        _resultsListBox.BeginUpdate();
        _resultsListBox.Items.Clear();
        foreach (LinkDisplayItem file in _visibleLinkedFiles)
        {
            _resultsListBox.Items.Add(file);
        }
        _resultsListBox.EndUpdate();

        _countLabel.Text = _visibleLinkedFiles.Count == 1
            ? "1 matching file"
            : $"{_visibleLinkedFiles.Count} matching files";
        _statusLabel.Text = statusText;
        UpdateSelectionActions();
        _resultsListBox.Invalidate();
    }

    private Control BuildRootLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            BackColor = NodeTieTheme.WindowBackground,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(28)));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildListArea(), 0, 1);
        root.Controls.Add(_statusLabel, 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.WindowBackground,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0, 0, 0, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 6; row++)
        {
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        header.Controls.Add(BuildTitleBar(), 0, 0);
        header.Controls.Add(BuildCurrentFileStrip(), 0, 1);
        header.Controls.Add(BuildInputRow("Find", _searchTextBox), 0, 2);
        header.Controls.Add(BuildSearchModeToggle(), 0, 3);
        header.Controls.Add(BuildInputRow("Paste", _linkTextBox), 0, 4);
        header.Controls.Add(BuildActions(), 0, 5);
        return header;
    }

    private Control BuildSearchModeToggle()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = NodeTieTheme.WindowBackground,
            Padding = new Padding(10, 4, 0, 2),
            Margin = new Padding(0)
        };
        panel.Controls.Add(_searchAllCheckBox);
        panel.Controls.Add(_searchModeHintLabel);
        return panel;
    }

    private Control BuildTitleBar()
    {
        Panel titleBar = new()
        {
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(38),
            BackColor = Color.FromArgb(11, 17, 27),
            Margin = new Padding(0, 0, 0, 6)
        };
        titleBar.MouseDown += DragTitleBar;

        Label title = new()
        {
            Text = "NODETIE",
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = NodeTieTheme.Accent,
            Location = new Point(LogicalToDeviceUnits(10), LogicalToDeviceUnits(9))
        };

        FlowLayoutPanel titleInfo = new()
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(LogicalToDeviceUnits(10), LogicalToDeviceUnits(7), 0, 0)
        };
        titleInfo.Controls.Add(title);
        titleInfo.Controls.Add(_countLabel);

        Button closeButton = new()
        {
            Text = "X",
            Dock = DockStyle.Right,
            Width = LogicalToDeviceUnits(38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = NodeTieTheme.TextSecondary,
            TabStop = false
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => Close();

        titleBar.Controls.Add(titleInfo);
        titleBar.Controls.Add(closeButton);
        return titleBar;
    }

    private Control BuildCurrentFileStrip()
    {
        TableLayoutPanel current = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.Surface,
            Padding = new Padding(12, 9, 12, 9),
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 6)
        };
        current.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        current.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        current.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        current.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        current.Controls.Add(_currentNameLabel, 0, 0);
        current.Controls.Add(_currentPathLabel, 0, 1);
        current.Controls.Add(_currentStateLabel, 0, 2);
        return current;
    }

    private Control BuildInputRow(string caption, TextBox input)
    {
        TableLayoutPanel row = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.Surface,
            Padding = new Padding(10, 6, 10, 6),
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 2)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(62)));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label label = new()
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = NodeTieTheme.Muted,
            Font = new Font("Segoe UI", 8.7f, FontStyle.Bold),
            Margin = new Padding(0)
        };
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(input, 1, 0);
        return row;
    }

    private Control BuildActions()
    {
        TableLayoutPanel actions = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.Surface,
            Padding = new Padding(8),
            ColumnCount = 6,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0)
        };
        for (int column = 0; column < 6; column++)
        {
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6f));
        }
        actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Button copyButton = CreateActionButton("Copy", NodeTieTheme.SurfaceAlt, NodeTieTheme.TextPrimary);
        copyButton.Click += (_, _) => CopyLinkRequested?.Invoke(this, EventArgs.Empty);

        Button linkButton = CreateActionButton("Link pasted", NodeTieTheme.Accent, Color.Black);
        linkButton.Click += (_, _) => LinkTextSubmitted?.Invoke(this, _linkTextBox.Text.Trim());

        actions.Controls.Add(copyButton, 0, 0);
        actions.Controls.Add(linkButton, 1, 0);
        actions.Controls.Add(_linkSelectedButton, 2, 0);
        actions.Controls.Add(_openSelectedButton, 3, 0);
        actions.Controls.Add(_removeSelectedButton, 4, 0);
        actions.Controls.Add(_bookmarkCurrentButton, 5, 0);
        return actions;
    }

    private Control BuildListArea()
    {
        TableLayoutPanel listArea = new()
        {
            Dock = DockStyle.Fill,
            BackColor = NodeTieTheme.Surface,
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        listArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        listArea.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(26)));
        listArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        listArea.Controls.Add(_listHeadingLabel, 0, 0);
        listArea.Controls.Add(_resultsListBox, 0, 1);

        ContextMenuStrip menu = new();
        ToolStripMenuItem openItem = new("Open selected");
        openItem.Click += (_, _) => TryRaiseLinkedFilesActivated();
        ToolStripMenuItem linkSelectedItem = new("Link selected");
        linkSelectedItem.Click += (_, _) => TryRaiseLinkKnownFilesRequested();
        ToolStripMenuItem removeItem = new("Remove selected");
        removeItem.Click += (_, _) => TryRaiseRemoveSelectedLinksRequested();
        menu.Items.Add(openItem);
        menu.Items.Add(linkSelectedItem);
        menu.Items.Add(removeItem);
        menu.Opening += (_, _) =>
        {
            bool hasSelection = SelectedLinkedFileIds.Count > 0;
            bool isGlobalMode = _searchAllCheckBox.Checked;
            openItem.Enabled = hasSelection;
            linkSelectedItem.Enabled = isGlobalMode && hasSelection;
            removeItem.Enabled = !isGlobalMode && hasSelection;
        };
        _resultsListBox.ContextMenuStrip = menu;

        return listArea;
    }

    private Button CreateActionButton(string text, Color background, Color foreground)
    {
        Button button = new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = LogicalToDeviceUnits(34),
            Margin = new Padding(3)
        };
        NodeTieTheme.ApplyFlatButton(button, background, foreground);
        button.AutoSize = false;
        button.MinimumSize = Size.Empty;
        return button;
    }

    private static Label CreateSingleLineLabel(Color color, Font font)
    {
        return new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            AutoEllipsis = true,
            Height = font.Height + 4,
            ForeColor = color,
            Font = font,
            Margin = new Padding(0)
        };
    }

    private void UpdateCurrentFile()
    {
        _currentNameLabel.Text = _selectedFile.DisplayName;
        _currentPathLabel.Text = $"{_selectedFile.CurrentPath}  |  Last seen: {FormatTimestamp(_selectedFile.UpdatedUtc)}";
        _currentStateLabel.Text = _selectedFile.IsMissing ? "Missing on disk" : "Available";
        _currentStateLabel.ForeColor = _selectedFile.IsMissing ? NodeTieTheme.Danger : NodeTieTheme.Accent;
    }

    private void ApplySearchFilter()
    {
        string term = _searchTextBox.Text.Trim();

        if (_searchAllCheckBox.Checked)
        {
            if (term.Length == 0)
            {
                ShowGlobalSearchResults([], "Global search is on. Type to search all known files.");
            }
            else
            {
                GlobalSearchRequested?.Invoke(this, term);
            }

            return;
        }

        IEnumerable<LinkDisplayItem> filtered = _allLinkedFiles;
        if (term.Length > 0)
        {
            filtered = filtered.Where(file =>
                file.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || file.Path.Contains(term, StringComparison.OrdinalIgnoreCase)
                || file.FileExtension.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        _visibleLinkedFiles.Clear();
        _visibleLinkedFiles.AddRange(filtered);

        _resultsListBox.BeginUpdate();
        _resultsListBox.Items.Clear();
        foreach (LinkDisplayItem file in _visibleLinkedFiles)
        {
            _resultsListBox.Items.Add(file);
        }
        _resultsListBox.EndUpdate();

        _countLabel.Text = _visibleLinkedFiles.Count == 1
            ? "1 linked file"
            : $"{_visibleLinkedFiles.Count} linked files";
        _statusLabel.Text = term.Length == 0
            ? _countLabel.Text
            : (_visibleLinkedFiles.Count == 0 ? "No linked files match the filter." : _countLabel.Text);
        UpdateSelectionActions();
        _resultsListBox.Invalidate();
    }

    private void OnSearchModeChanged()
    {
        bool isGlobalMode = _searchAllCheckBox.Checked;
        _searchTextBox.PlaceholderText = isGlobalMode
            ? "Search all known files"
            : "Search linked files";
        _searchModeHintLabel.Text = isGlobalMode
            ? "Global mode links search results to the current file."
            : "Local mode filters current links only.";
        _listHeadingLabel.Text = isGlobalMode ? "SEARCH RESULTS" : "LINKED FILES";
        UpdateSelectionActions();
        ApplySearchFilter();
    }

    private void DrawLinkedFileRow(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _visibleLinkedFiles.Count)
        {
            return;
        }

        LinkDisplayItem file = _visibleLinkedFiles[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color background = selected ? Color.FromArgb(37, 57, 82) : Color.FromArgb(14, 20, 31);
        using (SolidBrush brush = new(background))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        int horizontalPadding = LogicalToDeviceUnits(10);
        int verticalPadding = LogicalToDeviceUnits(5);
        Rectangle content = Rectangle.Inflate(e.Bounds, -horizontalPadding, -verticalPadding);
        Rectangle nameBounds = new(content.X, content.Y, content.Width, _rowNameFont.Height + 2);
        Rectangle pathBounds = new(content.X, content.Y + _rowNameFont.Height + 3, content.Width, _rowPathFont.Height + 2);

        TextRenderer.DrawText(
            e.Graphics,
            file.Name,
            _rowNameFont,
            nameBounds,
            file.IsMissing ? NodeTieTheme.Danger : NodeTieTheme.TextPrimary,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        string path = file.IsMissing ? $"Missing  |  {file.Path}" : file.Path;
        TextRenderer.DrawText(
            e.Graphics,
            path,
            _rowPathFont,
            pathBounds,
            file.IsMissing ? NodeTieTheme.Danger : NodeTieTheme.TextSecondary,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        using Pen separator = new(Color.FromArgb(35, 46, 64));
        e.Graphics.DrawLine(separator, content.Left, e.Bounds.Bottom - 1, content.Right, e.Bounds.Bottom - 1);
        e.DrawFocusRectangle();
    }

    private void UpdateRowHeight()
    {
        _resultsListBox.ItemHeight = _rowNameFont.Height + _rowPathFont.Height + LogicalToDeviceUnits(14);
        _resultsListBox.Invalidate();
    }

    private void UpdateSelectionActions()
    {
        bool hasSelection = SelectedLinkedFileIds.Count > 0;
        _openSelectedButton.Enabled = hasSelection;
        _linkSelectedButton.Enabled = _searchAllCheckBox.Checked && hasSelection;
        _removeSelectedButton.Enabled = !_searchAllCheckBox.Checked && hasSelection;
    }

    private void TryRaiseLinkKnownFilesRequested()
    {
        IReadOnlyList<long> selectedIds = SelectedLinkedFileIds;
        if (selectedIds.Count > 0)
        {
            LinkKnownFilesRequested?.Invoke(this, selectedIds);
        }
    }

    private void TryRaiseLinkedFilesActivated()
    {
        IReadOnlyList<long> selectedIds = SelectedLinkedFileIds;
        if (selectedIds.Count > 0)
        {
            LinkedFilesActivated?.Invoke(this, selectedIds);
        }
    }

    private void TryRaiseRemoveSelectedLinksRequested()
    {
        IReadOnlyList<long> selectedIds = SelectedLinkedFileIds;
        if (selectedIds.Count > 0)
        {
            RemoveSelectedLinksRequested?.Invoke(this, selectedIds);
        }
    }

    private void OnResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            if (_searchAllCheckBox.Checked)
            {
                TryRaiseLinkKnownFilesRequested();
            }
            else
            {
                TryRaiseLinkedFilesActivated();
            }
        }
        else if (e.KeyCode == Keys.Delete)
        {
            e.Handled = true;
            if (!_searchAllCheckBox.Checked)
            {
                TryRaiseRemoveSelectedLinksRequested();
            }
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Control && e.KeyCode == Keys.V)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            PasteClipboardRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void FitToCurrentScreen()
    {
        Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        int margin = LogicalToDeviceUnits(16);
        int width = Math.Min(LogicalToDeviceUnits(760), workingArea.Width - (margin * 2));
        int height = Math.Min(LogicalToDeviceUnits(620), workingArea.Height - (margin * 2));
        width = Math.Max(Math.Min(LogicalToDeviceUnits(620), workingArea.Width - (margin * 2)), width);
        height = Math.Max(Math.Min(LogicalToDeviceUnits(500), workingArea.Height - (margin * 2)), height);

        int x = Math.Clamp(Cursor.Position.X - (width / 2), workingArea.Left + margin, workingArea.Right - width - margin);
        int y = Math.Clamp(Cursor.Position.Y - LogicalToDeviceUnits(48), workingArea.Top + margin, workingArea.Bottom - height - margin);
        Bounds = new Rectangle(x, y, width, height);
    }

    private static Rectangle ConstrainBounds(Rectangle requested, Rectangle workingArea)
    {
        int width = Math.Min(requested.Width, workingArea.Width);
        int height = Math.Min(requested.Height, workingArea.Height);
        int x = Math.Clamp(requested.X, workingArea.Left, workingArea.Right - width);
        int y = Math.Clamp(requested.Y, workingArea.Top, workingArea.Bottom - height);
        return new Rectangle(x, y, width, height);
    }

    private void DragTitleBar(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private static string FormatTimestamp(string utcText)
    {
        if (DateTime.TryParse(utcText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
        {
            return parsed.ToLocalTime().ToString("g");
        }

        return DateTime.TryParse(utcText, out parsed)
            ? parsed.ToLocalTime().ToString("g")
            : utcText;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, int wParam, int lParam);
}
