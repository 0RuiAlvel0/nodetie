using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NodeTie.Infrastructure.Context;

public sealed class ForegroundWindowService : IForegroundWindowService
{
    public bool TryGetForegroundProcessName(out string processName)
    {
        processName = string.Empty;

        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        uint processId;
        GetWindowThreadProcessId(foregroundWindow, out processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}