using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Ui;

namespace NodeTie.Infrastructure.Linking;

public sealed class BookmarksWindowForm : Form
{
    private readonly ComboBox _viewComboBox;
    private readonly TextBox _searchTextBox;
    private readonly TextBox _tagFilterTextBox;
    private readonly ListBox _resultsListBox;
    private readonly Label _statusLabel;
    private readonly Label _countLabel;
    private readonly Button _openButton;
    private readonly Button _togglePinButton;
    private readonly Button _removeButton;

    private readonly List<BookmarkedLinkDisplayItem> _visibleItems = [];

    public BookmarksWindowForm()
    {
        Text = "NodeTie Bookmarks";
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = NodeTieTheme.DefaultFont;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(1160, 760);
        MinimumSize = new Size(980, 660);
        NodeTieTheme.ApplyForm(this);

        _viewComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0)
        };
        NodeTieTheme.ApplyComboBox(_viewComboBox);
        _viewComboBox.Items.AddRange(["All", "Pinned", "Recent"]);
        _viewComboBox.SelectedIndex = 0;
        _viewComboBox.SelectedIndexChanged += (_, _) => RaiseQueryRequested();

        _searchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Search bookmarks by name, path, or tag",
            Margin = new Padding(0)
        };
        NodeTieTheme.ApplyTextBox(_searchTextBox);
        _searchTextBox.TextChanged += (_, _) => RaiseQueryRequested();

        _tagFilterTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Tag filter (optional)",
            Margin = new Padding(0)
        };
        NodeTieTheme.ApplyTextBox(_tagFilterTextBox);
        _tagFilterTextBox.TextChanged += (_, _) => RaiseQueryRequested();

        _resultsListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.MultiExtended,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            BackColor = NodeTieTheme.Surface,
            ForeColor = NodeTieTheme.TextPrimary
        };
        _resultsListBox.SelectedIndexChanged += (_, _) => UpdateSelectionButtons();
        _resultsListBox.DoubleClick += (_, _) => OpenRequested?.Invoke(this, SelectedFileIds);
        _resultsListBox.KeyDown += OnResultsKeyDown;

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = NodeTieTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };

        _countLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = NodeTieTheme.TextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 4, 0)
        };

        _openButton = CreateButton("Open selected", NodeTieTheme.Accent, Color.Black);
        _openButton.Click += (_, _) => OpenRequested?.Invoke(this, SelectedFileIds);

        _togglePinButton = CreateButton("Toggle pin", NodeTieTheme.SurfaceAlt, NodeTieTheme.TextPrimary);
        _togglePinButton.Click += (_, _) => TogglePinRequested?.Invoke(this, SelectedFileIds);

        _removeButton = CreateButton("Delete bookmark", NodeTieTheme.Danger, Color.Black);
        _removeButton.Click += (_, _) => RemoveBookmarksRequested?.Invoke(this, SelectedFileIds);

        Button bookmarkCurrentButton = CreateButton("Bookmark current", NodeTieTheme.SurfaceAlt, NodeTieTheme.TextPrimary);
        bookmarkCurrentButton.Click += (_, _) => BookmarkCurrentRequested?.Invoke(this, EventArgs.Empty);

        Button addTagButton = CreateButton("Add tag", NodeTieTheme.SurfaceAlt, NodeTieTheme.TextPrimary);
        addTagButton.Click += (_, _) => PromptAndRaiseTagRequest(isAdd: true);

        Button removeTagButton = CreateButton("Untag", NodeTieTheme.SurfaceAlt, NodeTieTheme.TextPrimary);
        removeTagButton.Click += (_, _) => PromptAndRaiseTagRequest(isAdd: false);

        Controls.Add(BuildRootLayout(bookmarkCurrentButton, addTagButton, removeTagButton));

        Shown += (_, _) => RaiseQueryRequested();
        UpdateSelectionButtons();
    }

    public event EventHandler<(BookmarkView View, string Search, string TagFilter)>? QueryRequested;
    public event EventHandler<IReadOnlyList<long>>? OpenRequested;
    public event EventHandler<IReadOnlyList<long>>? TogglePinRequested;
    public event EventHandler<IReadOnlyList<long>>? RemoveBookmarksRequested;
    public event EventHandler<(IReadOnlyList<long> FileIds, string Tag)>? AddTagRequested;
    public event EventHandler<(IReadOnlyList<long> FileIds, string Tag)>? RemoveTagRequested;
    public event EventHandler? BookmarkCurrentRequested;

    public IReadOnlyList<long> SelectedFileIds => _resultsListBox.SelectedIndices
        .Cast<int>()
        .Where(index => index >= 0 && index < _visibleItems.Count)
        .Select(index => _visibleItems[index].FileId)
        .Distinct()
        .ToList();

    public void ShowResults(IReadOnlyList<BookmarkedLinkDisplayItem> items, string statusText)
    {
        _visibleItems.Clear();
        _visibleItems.AddRange(items);

        _resultsListBox.BeginUpdate();
        _resultsListBox.Items.Clear();
        foreach (BookmarkedLinkDisplayItem item in _visibleItems)
        {
            _resultsListBox.Items.Add(BuildRowText(item));
        }
        _resultsListBox.EndUpdate();

        _countLabel.Text = _visibleItems.Count == 1 ? "1 bookmark" : $"{_visibleItems.Count} bookmarks";
        _statusLabel.Text = statusText;
        UpdateSelectionButtons();
    }

    public BookmarkView SelectedView => _viewComboBox.SelectedIndex switch
    {
        1 => BookmarkView.Pinned,
        2 => BookmarkView.Recent,
        _ => BookmarkView.All
    };

    public string SearchTerm => _searchTextBox.Text.Trim();

    public string TagFilter => _tagFilterTextBox.Text.Trim();

    private Control BuildRootLayout(Button bookmarkCurrentButton, Button addTagButton, Button removeTagButton)
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            BackColor = NodeTieTheme.WindowBackground,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        root.Controls.Add(BuildFilters(), 0, 0);
        root.Controls.Add(BuildActions(bookmarkCurrentButton, addTagButton, removeTagButton), 0, 1);
        root.Controls.Add(_resultsListBox, 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
        return root;
    }

    private Control BuildFilters()
    {
        TableLayoutPanel filters = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.Surface,
            Padding = new Padding(8),
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8)
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));

        filters.Controls.Add(CreateHeaderLabel("View"), 0, 0);
        filters.Controls.Add(_viewComboBox, 1, 0);
        filters.Controls.Add(CreateHeaderLabel("Search"), 2, 0);
        filters.Controls.Add(_searchTextBox, 3, 0);
        filters.Controls.Add(CreateHeaderLabel("Tag"), 0, 1);
        filters.Controls.Add(_tagFilterTextBox, 1, 1);
        filters.SetColumnSpan(_tagFilterTextBox, 3);
        return filters;
    }

    private Control BuildActions(Button bookmarkCurrentButton, Button addTagButton, Button removeTagButton)
    {
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.Surface,
            Padding = new Padding(8),
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        actions.Controls.Add(bookmarkCurrentButton);
        actions.Controls.Add(_openButton);
        actions.Controls.Add(_togglePinButton);
        actions.Controls.Add(addTagButton);
        actions.Controls.Add(removeTagButton);
        actions.Controls.Add(_removeButton);
        return actions;
    }

    private Control BuildFooter()
    {
        TableLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            BackColor = NodeTieTheme.WindowBackground,
            ColumnCount = 2,
            RowCount = 1
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(_countLabel, 1, 0);
        return footer;
    }

    private static Label CreateHeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = NodeTieTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 6, 8, 0)
        };
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        Button button = new()
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(130, 36),
            Margin = new Padding(3)
        };
        NodeTieTheme.ApplyFlatButton(button, background, foreground);
        return button;
    }

    private void RaiseQueryRequested()
    {
        QueryRequested?.Invoke(this, (SelectedView, SearchTerm, TagFilter));
    }

    private void UpdateSelectionButtons()
    {
        bool hasSelection = SelectedFileIds.Count > 0;
        _openButton.Enabled = hasSelection;
        _togglePinButton.Enabled = hasSelection;
        _removeButton.Enabled = hasSelection;
    }

    private void PromptAndRaiseTagRequest(bool isAdd)
    {
        IReadOnlyList<long> selectedIds = SelectedFileIds;
        if (selectedIds.Count == 0)
        {
            return;
        }

        string? tag = PromptForTag(isAdd ? "Add tag" : "Untag");
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (isAdd)
        {
            AddTagRequested?.Invoke(this, (selectedIds, tag));
        }
        else
        {
            RemoveTagRequested?.Invoke(this, (selectedIds, tag));
        }
    }

    private void OnResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            OpenRequested?.Invoke(this, SelectedFileIds);
            return;
        }

        if (e.KeyCode == Keys.Delete)
        {
            e.Handled = true;
            RemoveBookmarksRequested?.Invoke(this, SelectedFileIds);
        }
    }

    private static string BuildRowText(BookmarkedLinkDisplayItem item)
    {
        string pinned = item.IsPinned ? "[PIN] " : string.Empty;
        string missing = item.IsMissing ? " (missing)" : string.Empty;
        string tags = item.Tags.Count == 0 ? "(no tags)" : string.Join(", ", item.Tags);
        string lastAccess = FormatTimestamp(item.LastAccessedUtc);
        return $"{pinned}{item.Name}{missing} | {item.Path} | Last opened: {lastAccess} | Tags: {tags}";
    }

    private string? PromptForTag(string caption)
    {
        using Form dialog = new()
        {
            Text = caption,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            ClientSize = new Size(460, 140)
        };

        Label label = new()
        {
            Text = "Tag:",
            Left = 10,
            Top = 12,
            Width = 48
        };
        TextBox textBox = new()
        {
            Left = 10,
            Top = 34,
            Width = 440
        };
        Button okButton = new()
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 294,
            Width = 75,
            Top = 90
        };
        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 375,
            Width = 75,
            Top = 90
        };

        dialog.Controls.Add(label);
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;
        dialog.Shown += (_, _) => textBox.Focus();

        return dialog.ShowDialog(this) == DialogResult.OK
            ? textBox.Text.Trim()
            : null;
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
}
