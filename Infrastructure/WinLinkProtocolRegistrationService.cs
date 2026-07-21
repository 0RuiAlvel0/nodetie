using System;
using Microsoft.Win32;

namespace NodeTie.Infrastructure;

public sealed class WinLinkProtocolRegistrationService
{
    private const string ProtocolKeyPath = @"Software\Classes\winlink";
    private const string CommandKeyPath = @"Software\Classes\winlink\shell\open\command";

    public bool TryEnsureRegistered(string executablePath, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            message = "Executable path is empty; winlink protocol was not registered.";
            return false;
        }

        try
        {
            using RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey(ProtocolKeyPath)
                ?? throw new InvalidOperationException("Could not create winlink protocol key.");
            protocolKey.SetValue(string.Empty, "URL:NodeTie Deep Link");
            protocolKey.SetValue("URL Protocol", string.Empty);

            using RegistryKey commandKey = Registry.CurrentUser.CreateSubKey(CommandKeyPath)
                ?? throw new InvalidOperationException("Could not create winlink command key.");
            commandKey.SetValue(string.Empty, BuildCommand(executablePath));

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
        return $"\"{executablePath}\" \"%1\"";
    }
}