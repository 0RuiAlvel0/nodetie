using System.Drawing;
using System.Windows.Forms;

namespace NodeTie.Infrastructure.Ui;

public static class NodeTieTheme
{
    public static readonly Color WindowBackground = Color.FromArgb(15, 23, 42);
    public static readonly Color Surface = Color.FromArgb(30, 41, 59);
    public static readonly Color SurfaceAlt = Color.FromArgb(37, 48, 68);
    public static readonly Color Border = Color.FromArgb(71, 85, 105);
    public static readonly Color Accent = Color.FromArgb(56, 189, 248);
    public static readonly Color AccentPressed = Color.FromArgb(14, 165, 233);
    public static readonly Color Danger = Color.FromArgb(248, 113, 113);
    public static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    public static readonly Color TextSecondary = Color.FromArgb(203, 213, 225);
    public static readonly Color Muted = Color.FromArgb(148, 163, 184);

    public static Font DefaultFont { get; } = new("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font TitleFont { get; } = new("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font SectionFont { get; } = new("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);

    public static void ApplyForm(Form form)
    {
        form.BackColor = WindowBackground;
        form.ForeColor = TextPrimary;
        form.Font = DefaultFont;
    }

    public static void ApplySurface(Control control)
    {
        control.BackColor = Surface;
        control.ForeColor = TextPrimary;
    }

    public static void ApplyFlatButton(Button button, Color? fill = null, Color? foreColor = null)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = fill ?? SurfaceAlt;
        button.ForeColor = foreColor ?? TextPrimary;
        button.Font = DefaultFont;
        button.Padding = new Padding(10, 7, 10, 7);
        button.AutoSize = true;
        button.MinimumSize = new Size(94, 36);
        button.Cursor = Cursors.Hand;
    }

    public static void ApplyTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Color.FromArgb(17, 24, 39);
        textBox.ForeColor = TextPrimary;
        textBox.Font = DefaultFont;
        textBox.Margin = new Padding(0);
    }

    public static void ApplyComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Color.FromArgb(17, 24, 39);
        comboBox.ForeColor = TextPrimary;
        comboBox.Font = DefaultFont;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
    }

    public static void ApplyListView(ListView listView)
    {
        listView.BackColor = Color.FromArgb(17, 24, 39);
        listView.ForeColor = TextPrimary;
        listView.BorderStyle = BorderStyle.FixedSingle;
        listView.Font = DefaultFont;
        listView.FullRowSelect = true;
        listView.HideSelection = false;
        listView.GridLines = false;
    }

    public static void ApplyLabel(Label label, bool secondary = false)
    {
        label.ForeColor = secondary ? TextSecondary : TextPrimary;
        label.Font = DefaultFont;
    }
}
