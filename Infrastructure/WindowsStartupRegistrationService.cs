using System;
using System.IO;
using Microsoft.Win32;

namespace NodeTie.Infrastructure;

public sealed class WindowsStartupRegistrationService
{
    private const string StartupShortcutFileName = "NodeTie.lnk";

    public bool IsEnabledForCurrentUser()
    {
        try
        {
            return File.Exists(GetStartupShortcutPath());
        }
        catch
        {
            return false;
        }
    }

    public bool TryEnsureRegistered(string executablePath, out string message)
    {
        return TrySetEnabled(executablePath, isEnabled: true, out message);
    }

    public bool TrySetEnabled(string executablePath, bool isEnabled, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            message = "Executable path is empty; startup registration was not updated.";
            return false;
        }

        try
        {
            string shortcutPath = GetStartupShortcutPath();

            if (isEnabled)
            {
                CreateStartupShortcut(executablePath, shortcutPath);
                return true;
            }

            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public static string GetStartupShortcutPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupShortcutFileName);
    }

    private static void CreateStartupShortcut(string executablePath, string shortcutPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath) ?? throw new InvalidOperationException("Could not resolve the startup folder."));

        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Could not create the Windows Script Host shell."))
            ?? throw new InvalidOperationException("Could not create the Windows Script Host shell.");

        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = executablePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;
        shortcut.Description = "Launch NodeTie when you sign in to Windows.";
        shortcut.Save();
    }
}
