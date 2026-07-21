using System;
using System.Windows.Forms;

namespace NodeTie.Infrastructure;

public sealed class NodeTieMessageWindow : NativeWindow, IDisposable
{
    public const int WmHotKey = 0x0312;
    public event EventHandler<int>? HotKeyPressed;

    public NodeTieMessageWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "NodeTieMessageWindow",
            X = 0,
            Y = 0,
            Width = 0,
            Height = 0,
            Style = 0,
            ExStyle = 0
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
            HotKeyPressed?.Invoke(this, m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
