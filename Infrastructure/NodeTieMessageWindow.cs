using System;
using System.Windows.Forms;

namespace NodeTie.Infrastructure;

public sealed class NodeTieMessageWindow : NativeWindow, IDisposable
{
    public const int WmHotKey = 0x0312;
    private bool _disposed;
    public event EventHandler<int>? HotKeyPressed;

    public NodeTieMessageWindow()
    {
        EnsureHandle();
    }

    public IntPtr EnsureHandle()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Handle != IntPtr.Zero)
        {
            return Handle;
        }

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

        return Handle;
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
        _disposed = true;
        DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
