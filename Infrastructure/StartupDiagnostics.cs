using System;
using System.IO;

namespace NodeTie.Infrastructure;

public static class StartupDiagnostics
{
    private static readonly object Sync = new();

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    public static void Exception(string context, Exception exception)
    {
        Write("ERROR", $"{context}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            NodeTiePaths.EnsureAppDataDirectoryExists();
            string logPath = NodeTiePaths.GetStartupLogPath();
            string line = $"{DateTime.UtcNow:O} [{level}] {message}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Diagnostics should never block app startup.
        }
    }
}