using System;
using System.IO;
using System.Windows.Forms;

namespace WuwaIniToolCs;

internal static class AppPaths
{
    public static string AppBaseDir => AppContext.BaseDirectory;

    public static string PresetsDir => Path.Combine(AppBaseDir, "presets");

    public static string AssetsDir => Path.Combine(AppBaseDir, "assets");

    public static string BackupsDir => Path.Combine(AppBaseDir, "backups");

    public static string AppCachePath => Path.Combine(AppBaseDir, AppConstants.AppCacheFile);

    public static string LegacyGameExeCachePath => Path.Combine(AppBaseDir, AppConstants.LegacyGameExeCacheFile);

    public static string LegacyRtCachePath => Path.Combine(AppBaseDir, AppConstants.LegacyRtCacheFile);

    public static string PermissionDebugLogPath => Path.Combine(AppBaseDir, AppConstants.PermissionDebugLogFile);

    public static string Win64ToEngineIni(string win64Dir)
    {
        return Path.Combine(win64Dir, "Engine.ini");
    }

    public static string? Win64ToGameExe(string win64Dir)
    {
        var exe = Path.Combine(win64Dir, AppConstants.GameExeName);
        return File.Exists(exe) ? exe : null;
    }

    public static bool IsValidWin64Folder(string? win64Dir)
    {
        if (string.IsNullOrWhiteSpace(win64Dir))
        {
            return false;
        }

        if (!Directory.Exists(win64Dir))
        {
            return false;
        }

        var gameExe = Path.Combine(win64Dir, AppConstants.GameExeName);
        return File.Exists(gameExe);
    }

    public static bool IsValidCustomConfigFolder(string? configDir)
    {
        if (string.IsNullOrWhiteSpace(configDir) || !Directory.Exists(configDir))
        {
            return false;
        }

        var normalized = Path.GetFullPath(configDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var suffix = Path.Combine("Saved", "Config", "WindowsNoEditor");
        return normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidTargetIniFolder(string? targetDir)
    {
        return IsValidWin64Folder(targetDir) || IsValidCustomConfigFolder(targetDir);
    }

    public static string TargetFolderToEngineIni(string targetDir)
    {
        return Path.Combine(targetDir, "Engine.ini");
    }

    public static string? GameExeToWin64(string? gameExe)
    {
        if (string.IsNullOrWhiteSpace(gameExe) || !File.Exists(gameExe))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(gameExe));
        if (string.IsNullOrWhiteSpace(dir))
        {
            return null;
        }

        return IsValidWin64Folder(dir) ? dir : null;
    }

    public static string DefaultWin64Folder()
    {
        return @"C:\Program Files\Wuthering Waves\Wuthering Waves Game\Client\Binaries\Win64";
    }

    public static void EnsureEngineIni(string engineIniPath)
    {
        var dir = Path.GetDirectoryName(engineIniPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(engineIniPath))
        {
            File.WriteAllText(engineIniPath, string.Empty);
        }
    }

    public static void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true,
            Verb = "open"
        });
    }
}
