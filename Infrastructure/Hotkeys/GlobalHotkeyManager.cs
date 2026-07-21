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

        // This is the OS-level registration that lets the app react even when unfocused.
        bool succeeded = RegisterHotKey(_window.Handle, _hotkeyId, (uint)binding.Modifiers, (uint)binding.Key);
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

        UnregisterHotKey(_window.Handle, _hotkeyId);
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
