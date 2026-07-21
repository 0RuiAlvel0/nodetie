using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NodeTie.Infrastructure.Ui;

namespace NodeTie.Infrastructure.Hotkeys;

public sealed class HotkeySettingsForm : Form
{
    private readonly HotkeyEditor _openPanelEditor;
    private readonly HotkeyEditor _copySelectionEditor;
    private readonly ComboBox _copyTargetComboBox;
    private readonly TableLayoutPanel _rootLayout;

    public HotkeySettingsForm(HotkeyBinding openPanelBinding, HotkeyBinding copySelectionBinding, CopyLinkTarget copyTarget)
    {
        Text = "NodeTie Settings";
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = NodeTieTheme.DefaultFont;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 640);
        NodeTieTheme.ApplyForm(this);

        _openPanelEditor = new HotkeyEditor(
            "Open link panel",
            "Opens linked files for the current File Explorer selection.");
        _copySelectionEditor = new HotkeyEditor(
            "Copy selection links",
            "Copies one or more selected Explorer files without opening the link panel.");
        _copyTargetComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0)
        };
        NodeTieTheme.ApplyComboBox(_copyTargetComboBox);
        _copyTargetComboBox.Items.Add("Obsidian");
        _copyTargetComboBox.Items.Add("OneNote");

        Button saveButton = CreateButton("Save", NodeTieTheme.Accent, Color.Black);
        Button cancelButton = CreateButton("Cancel", NodeTieTheme.SurfaceAlt, NodeTieTheme.TextPrimary);
        cancelButton.DialogResult = DialogResult.Cancel;
        saveButton.Click += (_, _) => SaveAndClose();

        _rootLayout = BuildRootLayout(saveButton, cancelButton);
        Controls.Add(_rootLayout);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        _openPanelEditor.ApplyBinding(openPanelBinding);
        _copySelectionEditor.ApplyBinding(copySelectionBinding);
        _copyTargetComboBox.SelectedItem = copyTarget == CopyLinkTarget.OneNote ? "OneNote" : "Obsidian";

        Shown += (_, _) => FitToWorkingArea();
    }

    public bool TryGetSelectedSettings(out HotkeyBinding openPanelBinding, out HotkeyBinding copySelectionBinding, out CopyLinkTarget copyTarget)
    {
        openPanelBinding = HotkeyBinding.Default;
        copySelectionBinding = HotkeySettingsService.DefaultCopySelectionBinding;
        copyTarget = HotkeySettingsService.DefaultCopyTarget;

        if (!_openPanelEditor.TryBuildBinding(out openPanelBinding)
            || !_copySelectionEditor.TryBuildBinding(out copySelectionBinding))
        {
            return false;
        }

        copyTarget = string.Equals(_copyTargetComboBox.SelectedItem as string, "OneNote", StringComparison.Ordinal)
            ? CopyLinkTarget.OneNote
            : CopyLinkTarget.Obsidian;

        return openPanelBinding != copySelectionBinding;
    }

    private TableLayoutPanel BuildRootLayout(Button saveButton, Button cancelButton)
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.WindowBackground,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(48)));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContent(), 0, 1);
        root.Controls.Add(BuildFooter(saveButton, cancelButton), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.Surface,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(6)));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Panel accent = new()
        {
            Dock = DockStyle.Fill,
            BackColor = NodeTieTheme.Accent,
            Margin = new Padding(0, 0, 12, 0)
        };

        Label title = new()
        {
            Text = "Global hotkeys",
            AutoSize = true,
            ForeColor = NodeTieTheme.TextPrimary,
            Font = NodeTieTheme.SectionFont,
            Margin = new Padding(0)
        };

        Label subtitle = new()
        {
            Text = "Configure separate shortcuts for opening NodeTie and copying Explorer selections.",
            AutoSize = true,
            MaximumSize = new Size(LogicalToDeviceUnits(620), 0),
            ForeColor = NodeTieTheme.TextSecondary,
            Margin = new Padding(0, 4, 0, 0)
        };

        header.Controls.Add(accent, 0, 0);
        header.SetRowSpan(accent, 2);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(subtitle, 1, 1);
        return header;
    }

    private Control BuildContent()
    {
        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.WindowBackground,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0, 0, 0, 8),
            Margin = new Padding(0)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.Controls.Add(_openPanelEditor.BuildControl(), 0, 0);
        content.Controls.Add(_copySelectionEditor.BuildControl(), 0, 1);
        content.Controls.Add(BuildCopyTargetControl(), 0, 2);

        Label hint = new()
        {
            Text = "The two shortcuts must be different. Defaults: Ctrl+Shift+L and Ctrl+Shift+K.",
            AutoSize = true,
            MaximumSize = new Size(LogicalToDeviceUnits(640), 0),
            ForeColor = NodeTieTheme.Muted,
            Margin = new Padding(4, 6, 4, 0)
        };
        content.Controls.Add(hint, 0, 3);

        return content;
    }

    private Control BuildFooter(Button saveButton, Button cancelButton)
    {
        Panel footer = new()
        {
            Dock = DockStyle.Fill,
            BackColor = NodeTieTheme.WindowBackground,
            Padding = new Padding(0, 6, 0, 0),
            Margin = new Padding(0)
        };

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0)
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        footer.Controls.Add(buttons);
        return footer;
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        Button button = new()
        {
            Text = text,
            AutoSize = true
        };
        NodeTieTheme.ApplyFlatButton(button, background, foreground);
        return button;
    }

    private void SaveAndClose()
    {
        if (!TryGetSelectedSettings(out _, out _, out _))
        {
            MessageBox.Show(
                this,
                "Choose a valid and different shortcut for each action.",
                "Invalid hotkeys",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private Control BuildCopyTargetControl()
    {
        TableLayoutPanel card = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = NodeTieTheme.Surface,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 8)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label label = new()
        {
            Text = "Copy target app",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = NodeTieTheme.TextPrimary,
            Font = NodeTieTheme.SectionFont,
            Margin = new Padding(0)
        };

        Label hint = new()
        {
            Text = "Obsidian copies markdown links; OneNote copies plain URI + rich HTML link data.",
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            ForeColor = NodeTieTheme.TextSecondary,
            Margin = new Padding(0, 6, 0, 0)
        };

        card.Controls.Add(label, 0, 0);
        card.Controls.Add(_copyTargetComboBox, 1, 0);
        card.Controls.Add(hint, 0, 1);
        card.SetColumnSpan(hint, 2);
        return card;
    }

    private void FitToWorkingArea()
    {
        Rectangle area = Screen.FromControl(this).WorkingArea;
        int margin = LogicalToDeviceUnits(16);
        int maximumWidth = Math.Max(1, area.Width - (margin * 2));
        int maximumHeight = Math.Max(1, area.Height - (margin * 2));

        int clientWidth = Math.Min(LogicalToDeviceUnits(760), maximumWidth);
        _rootLayout.Width = clientWidth;
        _rootLayout.PerformLayout();

        Size preferredClientSize = _rootLayout.GetPreferredSize(new Size(clientWidth, 0));
        int clientHeight = Math.Min(preferredClientSize.Height, maximumHeight);
        ClientSize = new Size(clientWidth, clientHeight);

        Location = new Point(
            area.Left + Math.Max(0, (area.Width - Width) / 2),
            area.Top + Math.Max(0, (area.Height - Height) / 2));
    }

    private sealed class HotkeyEditor
    {
        private readonly string _title;
        private readonly string _description;
        private readonly CheckBox _ctrlCheckBox;
        private readonly CheckBox _shiftCheckBox;
        private readonly CheckBox _altCheckBox;
        private readonly CheckBox _winCheckBox;
        private readonly ComboBox _keyComboBox;

        public HotkeyEditor(string title, string description)
        {
            _title = title;
            _description = description;
            _ctrlCheckBox = CreateModifierCheckBox("Ctrl");
            _shiftCheckBox = CreateModifierCheckBox("Shift");
            _altCheckBox = CreateModifierCheckBox("Alt");
            _winCheckBox = CreateModifierCheckBox("Win");

            _keyComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0)
            };
            NodeTieTheme.ApplyComboBox(_keyComboBox);
            _keyComboBox.Items.AddRange(HotkeyBinding.GetSupportedKeys().Select(key => key.ToString()).ToArray());
        }

        public Control BuildControl()
        {
            TableLayoutPanel card = new()
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = NodeTieTheme.Surface,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 8)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label title = new()
            {
                Text = _title,
                AutoSize = true,
                ForeColor = NodeTieTheme.TextPrimary,
                Font = NodeTieTheme.SectionFont,
                Margin = new Padding(0)
            };

            Label description = new()
            {
                Text = _description,
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = NodeTieTheme.TextSecondary,
                Margin = new Padding(0, 4, 0, 6)
            };

            FlowLayoutPanel modifiers = new()
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };
            modifiers.Controls.Add(_ctrlCheckBox);
            modifiers.Controls.Add(_shiftCheckBox);
            modifiers.Controls.Add(_altCheckBox);
            modifiers.Controls.Add(_winCheckBox);

            TableLayoutPanel keyRow = new()
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            keyRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label keyLabel = new()
            {
                Text = "Key",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = NodeTieTheme.TextPrimary,
                Font = NodeTieTheme.SectionFont,
                Margin = new Padding(0)
            };

            keyRow.Controls.Add(keyLabel, 0, 0);
            keyRow.Controls.Add(_keyComboBox, 1, 0);

            card.Controls.Add(title, 0, 0);
            card.Controls.Add(description, 0, 1);
            card.Controls.Add(modifiers, 0, 2);
            card.Controls.Add(keyRow, 0, 3);
            return card;
        }

        public void ApplyBinding(HotkeyBinding binding)
        {
            _ctrlCheckBox.Checked = binding.Modifiers.HasFlag(HotkeyModifiers.Control);
            _shiftCheckBox.Checked = binding.Modifiers.HasFlag(HotkeyModifiers.Shift);
            _altCheckBox.Checked = binding.Modifiers.HasFlag(HotkeyModifiers.Alt);
            _winCheckBox.Checked = binding.Modifiers.HasFlag(HotkeyModifiers.Win);

            int selectedIndex = _keyComboBox.Items.IndexOf(binding.Key.ToString());
            _keyComboBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        public bool TryBuildBinding(out HotkeyBinding binding)
        {
            binding = HotkeyBinding.Default;
            HotkeyModifiers modifiers = HotkeyModifiers.None;

            if (_ctrlCheckBox.Checked)
            {
                modifiers |= HotkeyModifiers.Control;
            }

            if (_shiftCheckBox.Checked)
            {
                modifiers |= HotkeyModifiers.Shift;
            }

            if (_altCheckBox.Checked)
            {
                modifiers |= HotkeyModifiers.Alt;
            }

            if (_winCheckBox.Checked)
            {
                modifiers |= HotkeyModifiers.Win;
            }

            if (modifiers == HotkeyModifiers.None
                || _keyComboBox.SelectedItem is not string keyText
                || !Enum.TryParse(keyText, out Keys key))
            {
                return false;
            }

            binding = new HotkeyBinding(modifiers, key);
            return true;
        }

        private static CheckBox CreateModifierCheckBox(string text)
        {
            return new CheckBox
            {
                Text = text,
                AutoSize = true,
                ForeColor = NodeTieTheme.TextPrimary,
                BackColor = NodeTieTheme.Surface,
                Margin = new Padding(0, 0, 18, 0)
            };
        }
    }
}
