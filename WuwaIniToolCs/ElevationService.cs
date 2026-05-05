using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace WuwaIniToolCs;

internal static class ElevationService
{
    public static bool IsAdmin()
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

    public static bool IsProtectedPath(string targetPath)
    {
        try
        {
            var full = Path.GetFullPath(targetPath).ToLowerInvariant();
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetEnvironmentVariable("SystemRoot") ?? string.Empty
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
        catch (UnauthorizedAccessException ex)
        {
            AppLogger.LogDetail("tool_is_protected_path", new Dictionary<string, string?>
            {
                ["target_path"] = targetPath,
                ["reason"] = "probe_unauthorized",
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString(),
                ["result"] = "true"
            });
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogDetail("tool_is_protected_path", new Dictionary<string, string?>
            {
                ["target_path"] = targetPath,
                ["reason"] = "probe_other_exception",
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString(),
                ["result"] = "false"
            });
            return false;
        }
    }

    public static bool RelaunchAsAdmin(params string[] args)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                AppLogger.LogDetail("tool_relaunch_admin", new Dictionary<string, string?>
                {
                    ["result"] = "false",
                    ["reason"] = "empty_process_path"
                });
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = AppPaths.AppBaseDir,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(" ", args)
            };
            Process.Start(psi);
            return true;
        }
        catch (Win32Exception ex)
        {
            AppLogger.LogDetail("tool_relaunch_admin", new Dictionary<string, string?>
            {
                ["result"] = "false",
                ["reason"] = "win32_exception",
                ["native_error_code"] = ex.NativeErrorCode.ToString(),
                ["message"] = ex.Message,
                ["exception"] = ex.ToString()
            });
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.LogDetail("tool_relaunch_admin", new Dictionary<string, string?>
            {
                ["result"] = "false",
                ["reason"] = "exception",
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString()
            });
            return false;
        }
    }

    public static bool EnsureAdminIfNeeded(bool needed, Func<bool> relaunchCallback)
    {
        var isWindows = OperatingSystem.IsWindows();
        var isAdmin = IsAdmin();
        if (!isWindows || !needed || isAdmin)
        {
            return false;
        }

        var ok = relaunchCallback();
        if (ok)
        {
            return true;
        }

        AppLogger.LogDetail("tool_ensure_admin", new Dictionary<string, string?>
        {
            ["needed"] = needed.ToString(),
            ["is_windows"] = isWindows.ToString(),
            ["is_admin"] = isAdmin.ToString(),
            ["action"] = "request_relaunch_failed"
        });
        
        PopupService.ShowError("오류", "관리자 권한 실행 요청에 실패했습니다.");
        return true;
    }
}
