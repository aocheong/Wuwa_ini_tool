using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WuwaIniToolCs;

internal static class EngineIniService
{
    public static string BuildLaunchArgs(string engineIniName)
    {
        var args = new List<string>(AppConstants.CommonGameLaunchArgs)
        {
            $"-EngineIni={engineIniName}"
        };
        return string.Join(" ", args.Select(EscapeArg));
    }

    public static void CleanupBeforeLaunch(string targetIni)
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

    public static string CreateBackup(string targetIni, string presetName)
    {
        Directory.CreateDirectory(AppPaths.BackupsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(AppPaths.BackupsDir, $"Engine_{timestamp}.ini");

        if (File.Exists(targetIni))
        {
            try
            {
                var content = File.ReadAllText(targetIni, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return backupPath;
                }
            }
            catch
            {
            }

            try
            {
                File.Copy(targetIni, backupPath, true);
                CleanupBackups(AppPaths.BackupsDir);
            }
            catch (Exception ex)
            {
                AppLogger.LogDetail("backup_copy_failed", new Dictionary<string, string?>
                {
                    ["target_ini"] = targetIni,
                    ["backup_path"] = backupPath,
                    ["exception_type"] = ex.GetType().Name,
                    ["exception"] = ex.ToString()
                });
            }
        }

        return backupPath;
    }

    public static bool TryCopyPresetIniWithRetry(
        string sourceIni,
        string targetIni,
        out Exception? lastError,
        out int attempts,
        int maxAttempts = 5,
        int delayMs = 350)
    {
        lastError = null;
        attempts = 0;

        var tryCount = Math.Max(1, maxAttempts);
        var wait = Math.Max(50, delayMs);

        for (var i = 1; i <= tryCount; i++)
        {
            attempts = i;
            TryClearReadOnly(targetIni);

            try
            {
                File.Copy(sourceIni, targetIni, true);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                lastError = ex;
                if (i < tryCount)
                {
                    Thread.Sleep(wait);
                    continue;
                }

                return false;
            }
        }

        return false;
    }

    public static void CleanupBackups(string backupSubdir)
    {
        try
        {
            var backups = new DirectoryInfo(backupSubdir)
                .GetFiles("Engine_*.ini")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            foreach (var oldFile in backups.Skip(AppConstants.MaxBackupCount))
            {
                try
                {
                    oldFile.Delete();
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    public static string DeleteCacheFolders(string savedDir)
    {
        var psoTargets = new[] { Path.Combine(savedDir, "PSO"), Path.Combine(savedDir, "PSOReport") };
        var shaderTargets = GetShaderCachePaths();

        var deletedPso = new List<string>();
        var missingPso = new List<string>();
        var deletedShader = new List<string>();
        var missingShader = new List<string>();

        foreach (var folder in psoTargets)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
                deletedPso.Add(Path.GetFileName(folder));
            }
            else
            {
                missingPso.Add(Path.GetFileName(folder));
            }
        }

        foreach (var folder in shaderTargets)
        {
            if (!Directory.Exists(folder))
            {
                missingShader.Add(folder);
                continue;
            }

            var removed = 0;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(folder))
                {
                    Directory.Delete(dir, true);
                    removed++;
                }

                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    File.Delete(file);
                    removed++;
                }

                deletedShader.Add($"{folder} (내부 {removed}개 정리)");
            }
            catch (Exception ex)
            {
                deletedShader.Add($"{folder} (일부 실패: {ex.Message})");
            }
        }

        var lines = new List<string>
        {
            $"대상 Saved 경로:\n{savedDir}",
            "",
            "[게임 캐시]"
        };

        if (deletedPso.Count > 0)
        {
            lines.Add($"삭제 완료: {string.Join(", ", deletedPso)}");
        }

        if (missingPso.Count > 0)
        {
            lines.Add($"미존재(건너뜀): {string.Join(", ", missingPso)}");
        }

        lines.Add("");
        lines.Add("[그래픽 셰이더 캐시]");

        if (deletedShader.Count > 0)
        {
            lines.Add("삭제 완료:");
            lines.AddRange(deletedShader.Select(static s => $"- {s}"));
        }
        else
        {
            lines.Add("삭제 완료: 없음");
        }

        if (missingShader.Count > 0)
        {
            lines.Add("미존재(건너뜀):");
            lines.AddRange(missingShader.Select(static s => $"- {s}"));
        }

        if (deletedPso.Count == 0 && deletedShader.Count == 0)
        {
            lines.Add("");
            lines.Add("삭제된 폴더가 없습니다.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static List<string> GetShaderCachePaths()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return new List<string>();
        }

        return new List<string>
        {
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "AMD", "GLCache"),
            Path.Combine(localAppData, "Intel", "ShaderCache")
        };
    }

    private static string EscapeArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        return arg.Contains(' ') ? $"\"{arg}\"" : arg;
    }

    private static void TryClearReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var attrs = File.GetAttributes(path);
            if ((attrs & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
        }
        catch
        {
        }
    }
}
