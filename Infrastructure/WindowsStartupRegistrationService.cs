using System;
using Microsoft.Win32;

namespace NodeTie.Infrastructure;

public sealed class WindowsStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "NodeTie";

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
            using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath)
                ?? throw new InvalidOperationException("Could not open the current-user startup Run key.");

            if (isEnabled)
            {
                runKey.SetValue(StartupValueName, BuildCommand(executablePath), RegistryValueKind.String);
                return true;
            }

            runKey.DeleteValue(StartupValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public static string BuildCommand(string executablePath)
    {
        return $"\"{executablePath}\"";
    }
}
