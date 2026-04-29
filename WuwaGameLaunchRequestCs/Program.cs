using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace WuwaGameLaunchRequestCs;

internal static class Program
{
    private const string GameExeName = "Client-Win64-Shipping.exe";
    private const string AppCacheFile = "app_cache.json";
    private const string LegacyGameExeCacheFile = "run_wuwa_cache.txt";
    private const string CacheKeyGameExePath = "game_exe_path";

    private static readonly string[] CommonGameLaunchArgs =
    {
        "-SkipSplash",
        "-NoVerifyGC",
        "-USEALLAVAILABLECORES"
    };

    [STAThread]
    private static int Main(string[] args)
    {
        if (TryHandleElevatedArgs(args))
        {
            return 0;
        }

        EnsureCacheFile();

        var gameExe = AutoFindGameExe();
        if (string.IsNullOrWhiteSpace(gameExe))
        {
            ShowError(
                "오류",
                "게임 실행 경로를 찾지 못했습니다.\n"
                + "먼저 최적화 툴에서 경로를 지정하거나, 기본 설치 경로/커스텀 설치 경로를 확인해 주세요.");
            return 1;
        }

        var win64Dir = GameExeToWin64(gameExe);
        if (string.IsNullOrWhiteSpace(win64Dir))
        {
            ShowError(
                "오류",
                "캐시된 게임 실행 경로가 유효한 Win64 폴더 구조가 아닙니다.\n"
                + "최적화 툴에서 경로를 다시 지정해 주세요.\n\n"
                + $"캐시 경로:\n{gameExe}");
            return 1;
        }

        var targetIni = Win64ToEngineIni(win64Dir);
        if (!File.Exists(targetIni))
        {
            ShowWarning(
                "안내",
                "Engine.ini 파일이 없습니다.\n"
                + "먼저 최적화 툴에서 프리셋 적용을 진행한 뒤 다시 실행해 주세요.");
            return 1;
        }

        if (EnsureAdminIfNeeded(
                IsProtectedPath(targetIni),
                () => RelaunchAsAdmin(new[] { "--elevated-launch", targetIni, gameExe })))
        {
            return 0;
        }

        try
        {
            CleanupEngineIniBeforeLaunch(targetIni);
            var argsText = BuildGameLaunchArgs(Path.GetFileName(targetIni));
            var result = NativeMethods.ShellExecuteW(
                IntPtr.Zero,
                "open",
                gameExe,
                argsText,
                Path.GetDirectoryName(gameExe),
                1);

            var code = result.ToInt64();
            if (code > 32)
            {
                return 0;
            }

            ShowError("오류", $"게임 실행에 실패했습니다. (코드: {code})");
            return 1;
        }
        catch (UnauthorizedAccessException)
        {
            ShowError(
                "Engine.ini 쓰기 실패",
                "게임 실행 전 Engine.ini를 수정하지 못했습니다.\n\n"
                + $"대상 파일:\n{targetIni}\n\n"
                + "파일이 읽기 전용이거나 다른 프로세스가 사용 중인지 확인해 주세요.");
            return 1;
        }
        catch (Exception ex)
        {
            ShowError("오류", $"게임 실행 중 문제가 발생했습니다.\n\n{ex.Message}");
            return 1;
        }
    }

    private static bool TryHandleElevatedArgs(string[] args)
    {
        if (args.Length < 1 || !string.Equals(args[0], "--elevated-launch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length < 3)
        {
            ShowError("오류", "관리자 실행 인자가 올바르지 않습니다.");
            return true;
        }

        var targetIni = args[1];
        var gameExe = args[2];

        try
        {
            if (!File.Exists(gameExe))
            {
                ShowError("오류", "게임 실행 파일이 존재하지 않습니다.\n실행 경로를 다시 확인해 주세요.");
                return true;
            }

            if (!File.Exists(targetIni))
            {
                ShowWarning("안내", "Engine.ini 파일이 없습니다.\n먼저 최적화 툴에서 프리셋 적용을 진행한 뒤 다시 실행해 주세요.");
                return true;
            }

            CleanupEngineIniBeforeLaunch(targetIni);
            Process.Start(new ProcessStartInfo
            {
                FileName = gameExe,
                Arguments = BuildGameLaunchArgs(Path.GetFileName(targetIni)),
                WorkingDirectory = Path.GetDirectoryName(gameExe),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError("오류", $"관리자 권한 실행 중 문제가 발생했습니다.\n\n{ex.Message}");
        }

        return true;
    }

    private static string AppBaseDir => AppContext.BaseDirectory;

    private static string AppCachePath => Path.Combine(AppBaseDir, AppCacheFile);

    private static string LegacyGameExeCachePath => Path.Combine(AppBaseDir, LegacyGameExeCacheFile);

    private static void EnsureCacheFile()
    {
        try
        {
            if (!File.Exists(AppCachePath))
            {
                File.WriteAllText(AppCachePath, "{}\n", Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    private static Dictionary<string, JsonElement> LoadCache()
    {
        try
        {
            if (!File.Exists(AppCachePath))
            {
                return new Dictionary<string, JsonElement>();
            }

            var raw = File.ReadAllText(AppCachePath, Encoding.UTF8).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Dictionary<string, JsonElement>();
            }

            using var doc = JsonDocument.Parse(raw);
            var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.Clone();
            }

            return dict;
        }
        catch
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static void SaveCacheValue<T>(string key, T value)
    {
        try
        {
            var current = LoadCache();
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
            current[key] = doc.RootElement.Clone();

            var plain = new Dictionary<string, object?>();
            foreach (var kv in current)
            {
                plain[kv.Key] = JsonSerializer.Deserialize<object>(kv.Value.GetRawText());
            }

            var json = JsonSerializer.Serialize(plain, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppCachePath, json + "\n", Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static void SaveGameExeCache(string gameExe)
    {
        SaveCacheValue(CacheKeyGameExePath, gameExe);
    }

    private static string? GetCachedGameExe()
    {
        try
        {
            var cache = LoadCache();
            if (cache.TryGetValue(CacheKeyGameExePath, out var element) && element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                {
                    return value;
                }
            }

            if (!File.Exists(LegacyGameExeCachePath))
            {
                return null;
            }

            var legacy = File.ReadAllText(LegacyGameExeCachePath, Encoding.UTF8).Trim();
            if (!string.IsNullOrWhiteSpace(legacy) && File.Exists(legacy))
            {
                SaveGameExeCache(legacy);
                return legacy;
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool IsValidWin64Folder(string? win64Dir)
    {
        if (string.IsNullOrWhiteSpace(win64Dir) || !Directory.Exists(win64Dir))
        {
            return false;
        }

        var gameExe = Path.Combine(win64Dir, GameExeName);
        return File.Exists(gameExe);
    }

    private static string? Win64ToGameExe(string win64Dir)
    {
        var candidate = Path.Combine(win64Dir, GameExeName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? GameExeToWin64(string gameExe)
    {
        if (string.IsNullOrWhiteSpace(gameExe) || !File.Exists(gameExe))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(gameExe));
        return IsValidWin64Folder(dir) ? dir : null;
    }

    private static string Win64ToEngineIni(string win64Dir)
    {
        return Path.Combine(win64Dir, "Engine.ini");
    }

    private static List<string> Win64FolderCandidates()
    {
        var candidates = new List<string>
        {
            @"C:\Program Files\Wuthering Waves\Wuthering Waves Game\Client\Binaries\Win64"
        };

        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");

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
        foreach (var v in new[] { programFiles, programFilesX86, localAppData })
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                baseDirs.Add(v);
            }
        }

        var gameFolders = new[]
        {
            "Wuthering Waves",
            "WutheringWaves",
            @"Epic Games\Wuthering Waves",
            @"Epic Games\WutheringWaves",
            @"Google\Play Games\Games\Wuthering Waves",
            @"Google\Play Games\Games\WutheringWaves"
        };

        var rels = new[]
        {
            @"Wuthering Waves Game\Client\Binaries\Win64",
            @"Client\Binaries\Win64",
            @"Binaries\Win64"
        };

        foreach (var b in baseDirs)
        {
            foreach (var f in gameFolders)
            {
                foreach (var r in rels)
                {
                    candidates.Add(Path.Combine(b, f, r));
                }
            }
        }

        foreach (var s in steamBases)
        {
            foreach (var f in new[] { "Wuthering Waves", "WutheringWaves" })
            {
                foreach (var r in rels)
                {
                    candidates.Add(Path.Combine(s, f, r));
                }
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? AutoFindWin64Folder()
    {
        foreach (var candidate in Win64FolderCandidates())
        {
            if (IsValidWin64Folder(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static List<string> GameExeCandidates()
    {
        var candidates = new List<string>();
        var rels = new[]
        {
            Path.Combine("Wuthering Waves Game", "Client", "Binaries", "Win64", GameExeName),
            Path.Combine("Client", "Binaries", "Win64", GameExeName),
            GameExeName
        };

        var baseDirs = new List<string>();
        foreach (var env in new[] { "ProgramFiles", "ProgramFiles(x86)", "LOCALAPPDATA", "ProgramData" })
        {
            var value = Environment.GetEnvironmentVariable(env);
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

        var folders = new[]
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

        foreach (var b in baseDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var f in folders)
            {
                foreach (var r in rels)
                {
                    candidates.Add(Path.Combine(b, f, r));
                }
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? ScanCustomWin64Folder(int maxDepth = 5)
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
                var c = Path.Combine(root, name);
                if (Directory.Exists(c))
                {
                    roots.Add(c);
                }
            }
        }

        var uniqueRoots = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$recycle.bin", "system volume information", "windows", "programdata",
            "recovery", "msocache", "intel", "amd", "nvidia", "onedrivetemp"
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
                    if (IsValidWin64Folder(guessed))
                    {
                        return guessed;
                    }

                    var fallback = Path.Combine(entry, "Client", "Binaries", "Win64");
                    if (IsValidWin64Folder(fallback))
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

    private static string? AutoFindGameExe()
    {
        var cached = GetCachedGameExe();
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        foreach (var candidate in GameExeCandidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            SaveGameExeCache(candidate);
            return candidate;
        }

        var win64 = AutoFindWin64Folder() ?? ScanCustomWin64Folder();
        if (!string.IsNullOrWhiteSpace(win64))
        {
            var fromWin64 = Win64ToGameExe(win64);
            if (!string.IsNullOrWhiteSpace(fromWin64))
            {
                SaveGameExeCache(fromWin64);
                return fromWin64;
            }
        }

        return null;
    }

    private static string BuildGameLaunchArgs(string engineIniName)
    {
        var args = new List<string>(CommonGameLaunchArgs)
        {
            $"-EngineIni={engineIniName}"
        };
        return string.Join(" ", args.Select(QuoteArg));
    }

    private static void CleanupEngineIniBeforeLaunch(string targetIni)
    {
        if (!File.Exists(targetIni))
        {
            return;
        }

        var lines = File.ReadAllLines(targetIni, Encoding.UTF8);
        var keep = false;
        var notSys = false;
        var output = new List<string>();

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower == "[systemsettings]" || lower == "[core.log]" || lower == "[/script/engine.renderersettings]")
            {
                keep = true;
                if (lower == "[core.log]" || lower == "[/script/engine.renderersettings]")
                {
                    notSys = true;
                }
            }

            if (!keep)
            {
                continue;
            }

            if (notSys && lower.StartsWith("r.raytracing.loadconfig", StringComparison.Ordinal))
            {
                output.Add(line);
                keep = false;
                continue;
            }

            if (line.Length == 0 && notSys)
            {
                keep = false;
                notSys = false;
                continue;
            }

            output.Add(line);
        }

        TryClearReadOnly(targetIni);
        File.WriteAllText(targetIni, string.Join(Environment.NewLine, output) + Environment.NewLine, Encoding.UTF8);
    }

    private static void TryClearReadOnly(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var attrs = File.GetAttributes(filePath);
            if ((attrs & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(filePath, attrs & ~FileAttributes.ReadOnly);
            }
        }
        catch
        {
        }
    }

    private static bool IsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProtectedPath(string targetPath)
    {
        try
        {
            var full = Path.GetFullPath(targetPath).ToLowerInvariant();
            var roots = new[]
            {
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                Environment.GetEnvironmentVariable("SystemRoot")
            };

            foreach (var root in roots)
            {
                if (!string.IsNullOrWhiteSpace(root) && full.StartsWith(Path.GetFullPath(root).ToLowerInvariant(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            if (IsAdmin())
            {
                return false;
            }

            var dir = Path.GetDirectoryName(full);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return false;
            }

            var probe = Path.Combine(dir, $".wuwa_uac_probe_{Environment.ProcessId}.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool RelaunchAsAdmin(IEnumerable<string> args)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = AppBaseDir,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(" ", args.Select(QuoteArg))
            };

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool EnsureAdminIfNeeded(bool needed, Func<bool> relaunchCallback)
    {
        if (!OperatingSystem.IsWindows() || !needed || IsAdmin())
        {
            return false;
        }

        if (relaunchCallback())
        {
            return true;
        }

        ShowError("오류", "관리자 권한 실행 요청에 실패했습니다.");
        return true;
    }

    private static void ShowWarning(string title, string message)
    {
        NativeMethods.ShowMessage(title, message, NativeMethods.MB_OK | NativeMethods.MB_ICONWARNING, null);
    }

    private static void ShowError(string title, string message)
    {
        NativeMethods.ShowMessage(title, message, NativeMethods.MB_OK | NativeMethods.MB_ICONERROR, null);
    }

    private static string QuoteArg(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return "\"\"";
        }

        return arg.Contains(' ') ? $"\"{arg}\"" : arg;
    }

    private static class NativeMethods
    {
        public const uint MB_OK = 0x00000000;
        public const uint MB_ICONINFORMATION = 0x00000040;
        public const uint MB_ICONWARNING = 0x00000030;
        public const uint MB_ICONERROR = 0x00000010;
        private const uint MB_SETFOREGROUND = 0x00010000;
        private const uint MB_TOPMOST = 0x00040000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr ShellExecuteW(
            IntPtr hwnd,
            string lpOperation,
            string lpFile,
            string? lpParameters,
            string? lpDirectory,
            int nShowCmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBoxW(
            IntPtr hWnd,
            string lpText,
            string lpCaption,
            uint uType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxTimeoutW", SetLastError = true)]
        private static extern int MessageBoxTimeoutW(
            IntPtr hWnd,
            string lpText,
            string lpCaption,
            uint uType,
            short wLanguageId,
            int dwMilliseconds);

        public static void ShowMessage(string title, string message, uint flags, int? timeoutMs)
        {
            var finalFlags = flags | MB_SETFOREGROUND | MB_TOPMOST;
            if (timeoutMs.HasValue)
            {
                try
                {
                    MessageBoxTimeoutW(IntPtr.Zero, message, title, finalFlags, 0, timeoutMs.Value);
                    return;
                }
                catch
                {
                }
            }

            MessageBoxW(IntPtr.Zero, message, title, finalFlags);
        }
    }
}
