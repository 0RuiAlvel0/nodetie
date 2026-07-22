using System;
using System.IO;

namespace NodeTie.Infrastructure;

public static class NodeTiePaths
{
    private const string ApplicationFolderName = "NodeTie";
    private const string DatabaseFileName = "nodetie.db";
    private const string StartupLogFileName = "startup.log";

    public static string GetAppDataDirectory()
    {
        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDirectory, ApplicationFolderName);
    }

    public static string GetDatabasePath()
    {
        return Path.Combine(GetAppDataDirectory(), DatabaseFileName);
    }

    public static string GetStartupLogPath()
    {
        return Path.Combine(GetAppDataDirectory(), StartupLogFileName);
    }

    public static void EnsureAppDataDirectoryExists()
    {
        Directory.CreateDirectory(GetAppDataDirectory());
    }
}
