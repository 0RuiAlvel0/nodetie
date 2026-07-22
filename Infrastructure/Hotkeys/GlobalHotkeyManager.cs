using System;
using System.Runtime.InteropServices;
using NodeTie.Infrastructure;

namespace NodeTie.Infrastructure.Hotkeys;

public sealed class GlobalHotkeyManager : IDisposable
{
    private readonly NodeTieMessageWindow _window;
    private readonly int _hotkeyId;
    private bool _isRegistered;

    public GlobalHotkeyManager(NodeTieMessageWindow window, int hotkeyId)
    {
        _window = window;
        _hotkeyId = hotkeyId;
    }

    public int HotkeyId => _hotkeyId;

    public HotkeyBinding? CurrentBinding { get; private set; }

    public bool Register(HotkeyBinding binding)
    {
        Unregister();

        IntPtr windowHandle;
        try
        {
            windowHandle = _window.EnsureHandle();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        // This is the OS-level registration that lets the app react even when unfocused.
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        bool succeeded = RegisterHotKey(windowHandle, _hotkeyId, (uint)binding.Modifiers, (uint)binding.Key);
        if (!succeeded)
        {
            return false;
        }

        _isRegistered = true;
        CurrentBinding = binding;
        return true;
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        try
        {
            IntPtr windowHandle = _window.EnsureHandle();
            if (windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(windowHandle, _hotkeyId);
            }
        }
        catch (ObjectDisposedException)
        {
        }

        _isRegistered = false;
        CurrentBinding = null;
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
