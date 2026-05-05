using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WuwaIniToolCs;

internal static class GamePathService
{
    public static string? FindWin64FolderWithFallbackDepth()
    {
        return AutoFindWin64Folder()
            ?? ScanCustomWin64Folder(maxDepth: 3)
            ?? ScanCustomWin64Folder(maxDepth: 5);
    }

    public static string? FindCustomConfigFolderWithFallbackDepth()
    {
        return AutoFindCustomConfigFolder()
            ?? ScanCustomConfigFolder(maxDepth: 3)
            ?? ScanCustomConfigFolder(maxDepth: 5);
    }

    public static List<string> Win64CandidateFolders()
    {
        var candidates = new List<string>
        {
            @"C:\Program Files\Wuthering Waves\Wuthering Waves Game\Client\Binaries\Win64"
        };

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var steamBases = new List<string>();
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            steamBases.Add(Path.Combine(programFiles, "Steam", "steamapps", "common"));
        }

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            steamBases.Add(Path.Combine(programFilesX86, "Steam", "steamapps", "common"));
        }

        var baseDirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            baseDirs.Add(programFiles);
        }

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            baseDirs.Add(programFilesX86);
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            baseDirs.Add(localAppData);
        }

        var gameFolderCandidates = new[]
        {
            "Wuthering Waves",
            "WutheringWaves",
            @"Epic Games\Wuthering Waves",
            @"Epic Games\WutheringWaves",
            @"Google\Play Games\Games\Wuthering Waves",
            @"Google\Play Games\Games\WutheringWaves"
        };

        var relativePatterns = new[]
        {
            @"Wuthering Waves Game\Client\Binaries\Win64",
            @"Client\Binaries\Win64",
            @"Binaries\Win64"
        };

        foreach (var baseDir in baseDirs)
        {
            foreach (var folder in gameFolderCandidates)
            {
                foreach (var rel in relativePatterns)
                {
                    candidates.Add(Path.Combine(baseDir, folder, rel));
                }
            }
        }

        var steamGameFolders = new[] { "Wuthering Waves", "WutheringWaves" };
        foreach (var steamBase in steamBases)
        {
            foreach (var folder in steamGameFolders)
            {
                foreach (var rel in relativePatterns)
                {
                    candidates.Add(Path.Combine(steamBase, folder, rel));
                }
            }
        }

        return candidates
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? AutoFindWin64Folder()
    {
        foreach (var candidate in Win64CandidateFolders())
        {
            if (AppPaths.IsValidWin64Folder(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static List<string> CustomConfigCandidateFolders()
    {
        var candidates = new List<string>
        {
            @"C:\Program Files\Wuthering Waves\Wuthering Waves Game\Client\Saved\Config\WindowsNoEditor"
        };

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var steamBases = new List<string>();
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            steamBases.Add(Path.Combine(programFiles, "Steam", "steamapps", "common"));
        }

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            steamBases.Add(Path.Combine(programFilesX86, "Steam", "steamapps", "common"));
        }

        var baseDirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            baseDirs.Add(programFiles);
        }

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            baseDirs.Add(programFilesX86);
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            baseDirs.Add(localAppData);
        }

        var gameFolderCandidates = new[]
        {
            "Wuthering Waves",
            "WutheringWaves",
            @"Epic Games\Wuthering Waves",
            @"Epic Games\WutheringWaves",
            @"Google\Play Games\Games\Wuthering Waves",
            @"Google\Play Games\Games\WutheringWaves"
        };

        var relativePatterns = new[]
        {
            @"Wuthering Waves Game\Client\Saved\Config\WindowsNoEditor",
            @"Client\Saved\Config\WindowsNoEditor",
            @"Saved\Config\WindowsNoEditor"
        };

        foreach (var baseDir in baseDirs)
        {
            foreach (var folder in gameFolderCandidates)
            {
                foreach (var rel in relativePatterns)
                {
                    candidates.Add(Path.Combine(baseDir, folder, rel));
                }
            }
        }

        var steamGameFolders = new[] { "Wuthering Waves", "WutheringWaves" };
        foreach (var steamBase in steamBases)
        {
            foreach (var folder in steamGameFolders)
            {
                foreach (var rel in relativePatterns)
                {
                    candidates.Add(Path.Combine(steamBase, folder, rel));
                }
            }
        }

        return candidates
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? AutoFindCustomConfigFolder()
    {
        foreach (var candidate in CustomConfigCandidateFolders())
        {
            if (AppPaths.IsValidCustomConfigFolder(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? FindGameExe(string? targetWin64Dir)
    {
        if (!string.IsNullOrWhiteSpace(targetWin64Dir))
        {
            var fromWin64 = AppPaths.Win64ToGameExe(targetWin64Dir);
            if (!string.IsNullOrWhiteSpace(fromWin64))
            {
                CacheService.SaveGameExePath(fromWin64);
                return fromWin64;
            }
        }

        var cached = CacheService.GetCachedGameExePath();
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        foreach (var candidate in GameExeCandidates())
        {
            if (File.Exists(candidate))
            {
                CacheService.SaveGameExePath(candidate);
                return candidate;
            }
        }

        var foundWin64 = FindWin64FolderWithFallbackDepth();
        if (!string.IsNullOrWhiteSpace(foundWin64))
        {
            var fromFound = AppPaths.Win64ToGameExe(foundWin64);
            if (!string.IsNullOrWhiteSpace(fromFound))
            {
                CacheService.SaveGameExePath(fromFound);
                return fromFound;
            }
        }

        return null;
    }

    public static string? ScanCustomWin64Folder(int maxDepth = 5)
    {
        var roots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            var root = drive.RootDirectory.FullName;
            roots.Add(root);

            foreach (var name in new[] { "Games", "game", "GAME" })
            {
                var preferred = Path.Combine(root, name);
                if (Directory.Exists(preferred))
                {
                    roots.Add(preferred);
                }
            }
        }

        var uniqueRoots = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$recycle.bin",
            "system volume information",
            "windows",
            "programdata",
            "recovery",
            "msocache",
            "intel",
            "amd",
            "nvidia",
            "onedrivetemp"
        };

        var hints = new[] { "wuthering", "waves", "wuwa" };

        string? Scan(string current, int depth)
        {
            if (depth > maxDepth)
            {
                return null;
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateDirectories(current);
            }
            catch
            {
                return null;
            }

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (skipNames.Contains(name))
                {
                    continue;
                }

                var lower = name.ToLowerInvariant();
                if (hints.Any(lower.Contains))
                {
                    var guessed = Path.Combine(entry, "Wuthering Waves Game", "Client", "Binaries", "Win64");
                    if (AppPaths.IsValidWin64Folder(guessed))
                    {
                        return guessed;
                    }

                    var fallback = Path.Combine(entry, "Client", "Binaries", "Win64");
                    if (AppPaths.IsValidWin64Folder(fallback))
                    {
                        return fallback;
                    }
                }

                var found = Scan(entry, depth + 1);
                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }

            return null;
        }

        foreach (var root in uniqueRoots)
        {
            var found = Scan(root, 0);
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        return null;
    }

    public static string? ScanCustomConfigFolder(int maxDepth = 5)
    {
        var roots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            var root = drive.RootDirectory.FullName;
            roots.Add(root);

            foreach (var name in new[] { "Games", "game", "GAME" })
            {
                var preferred = Path.Combine(root, name);
                if (Directory.Exists(preferred))
                {
                    roots.Add(preferred);
                }
            }
        }

        var uniqueRoots = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$recycle.bin",
            "system volume information",
            "windows",
            "programdata",
            "recovery",
            "msocache",
            "intel",
            "amd",
            "nvidia",
            "onedrivetemp"
        };

        var hints = new[] { "wuthering", "waves", "wuwa" };

        string? Scan(string current, int depth)
        {
            if (depth > maxDepth)
            {
                return null;
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateDirectories(current);
            }
            catch
            {
                return null;
            }

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (skipNames.Contains(name))
                {
                    continue;
                }

                var lower = name.ToLowerInvariant();
                if (hints.Any(lower.Contains))
                {
                    var guessed = Path.Combine(entry, "Wuthering Waves Game", "Client", "Saved", "Config", "WindowsNoEditor");
                    if (AppPaths.IsValidCustomConfigFolder(guessed))
                    {
                        return guessed;
                    }

                    var fallback = Path.Combine(entry, "Client", "Saved", "Config", "WindowsNoEditor");
                    if (AppPaths.IsValidCustomConfigFolder(fallback))
                    {
                        return fallback;
                    }
                }

                var found = Scan(entry, depth + 1);
                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }

            return null;
        }

        foreach (var root in uniqueRoots)
        {
            var found = Scan(root, 0);
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        return null;
    }

    private static List<string> GameExeCandidates()
    {
        var candidates = new List<string>();
        var relativePatterns = new[]
        {
            Path.Combine("Wuthering Waves Game", "Client", "Binaries", "Win64", AppConstants.GameExeName),
            Path.Combine("Client", "Binaries", "Win64", AppConstants.GameExeName),
            AppConstants.GameExeName
        };

        var baseDirs = new List<string>();
        foreach (var envName in new[] { "ProgramFiles", "ProgramFiles(x86)", "LOCALAPPDATA", "ProgramData" })
        {
            var value = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                baseDirs.Add(value);
            }
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                baseDirs.Add(drive.RootDirectory.FullName);
            }
        }

        var folderCandidates = new[]
        {
            "Wuthering Waves",
            "Wuthering Waves Game",
            "WutheringWaves",
            "명조",
            @"Epic Games\WutheringWavesj3oFh",
            @"Epic Games\Wuthering Waves",
            @"Games\Wuthering Waves",
            @"Game\Wuthering Waves"
        };

        foreach (var baseDir in baseDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var folder in folderCandidates)
            {
                foreach (var rel in relativePatterns)
                {
                    candidates.Add(Path.Combine(baseDir, folder, rel));
                }
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
