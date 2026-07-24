using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NodeTie.Infrastructure;

public sealed class NodeTieMessageWindow : NativeWindow, IDisposable
{
    public const int WmHotKey = 0x0312;
    public const string WindowCaption = "NodeTieMessageWindow";
    private const int WmCopyData = 0x004A;
    private bool _disposed;
    public event EventHandler<int>? HotKeyPressed;
    public event EventHandler<string>? DeepLinkReceived;

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
            Caption = WindowCaption,
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
        else if (m.Msg == WmCopyData && TryReadCopyDataMessage(m.LParam, out string deepLink))
        {
            DeepLinkReceived?.Invoke(this, deepLink);
        }

        base.WndProc(ref m);
    }

    private static bool TryReadCopyDataMessage(IntPtr lParam, out string payload)
    {
        payload = string.Empty;
        if (lParam == IntPtr.Zero)
        {
            return false;
        }

        CopyDataStruct copyData = Marshal.PtrToStructure<CopyDataStruct>(lParam);
        if (copyData.CbData <= 0 || copyData.LpData == IntPtr.Zero)
        {
            return false;
        }

        string? text = Marshal.PtrToStringUni(copyData.LpData, copyData.CbData / sizeof(char));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        payload = text.TrimEnd('\0').Trim();
        return !string.IsNullOrWhiteSpace(payload);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CopyDataStruct
    {
        public IntPtr DwData { get; init; }
        public int CbData { get; init; }
        public IntPtr LpData { get; init; }
    }

    public void Dispose()
    {
        _disposed = true;
        DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
