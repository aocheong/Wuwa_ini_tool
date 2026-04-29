using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace WuwaIniToolCs;

internal static class ElevatedMode
{
    public static bool TryHandle(string[] args)
    {
        if (args.Length == 0 || !args[0].StartsWith("--elevated-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            if (args.Length >= 4 && args[0] == "--elevated-copy")
            {
                var sourceIni = args[1];
                var targetIni = args[2];
                var presetName = args[3];

                AppPaths.EnsureEngineIni(targetIni);
                var backupPath = EngineIniService.CreateBackup(targetIni, presetName);

                var copied = EngineIniService.TryCopyPresetIniWithRetry(
                    sourceIni,
                    targetIni,
                    out var copyError,
                    out var copyAttempts);

                if (!copied)
                {
                    AppLogger.LogDetail("elevated_copy_permission_error", new Dictionary<string, string?>
                    {
                        ["source_ini"] = sourceIni,
                        ["target_ini"] = targetIni,
                        ["preset_name"] = presetName,
                        ["attempts"] = copyAttempts.ToString(),
                        ["exception_type"] = copyError?.GetType().Name,
                        ["exception"] = copyError?.ToString()
                    });

                    PopupService.ShowError(
                        "권한 오류",
                        "파일 쓰기 권한이 없거나 파일이 사용 중입니다.\n"
                        + $"{copyAttempts}회 재시도했지만 적용에 실패했습니다.\n"
                        + "게임/런처를 완전히 종료한 뒤 다시 시도해 주세요.\n"
                        + "백신/랜섬웨어 보호의 차단 기록도 함께 확인해 주세요.\n\n"
                        + $"대상 파일:\n{targetIni}\n\n"
                        + $"상세 오류:\n{copyError?.Message}");
                    return true;
                }

                var backupSummary = File.Exists(backupPath)
                    ? $"백업 파일:\n{backupPath}"
                    : "백업할 기존 Engine.ini가 없어 백업은 건너뛰었습니다.";

                PopupService.ShowInfo(
                    "완료",
                    $"[{presetName}] 적용이 완료되었습니다.\n\n대상 파일:\n{targetIni}\n\n{backupSummary}");

                var targetDir = Path.GetDirectoryName(targetIni) ?? string.Empty;
                var savedDir = ResolveSavedDir(targetDir);
                if (PopupService.ConfirmYesNo(
                        "캐시 제거 권장",
                        $"캐시 적중 이슈를 방지하기 위해 캐시 제거를 권장합니다.\n"
                        + "지금 캐시(PSO/PSOReport + 셰이더 캐시)를 제거할까요?\n\n"
                        + $"기준 Saved 경로:\n{savedDir}") == DialogResult.Yes)
                {
                    var result = EngineIniService.DeleteCacheFolders(savedDir);
                    PopupService.ShowInfo("완료", result);
                    PopupService.ShowCacheRebuildNotice();
                }

                return true;
            }

            if (args.Length >= 3 && args[0] == "--elevated-launch")
            {
                var targetIni = args[1];
                var gameExe = args[2];
                try
                {
                    if (!File.Exists(gameExe))
                    {
                        PopupService.ShowError("오류", "게임 실행 파일을 찾지 못했습니다.\n자동 탐색 또는 폴더 선택으로 Win64 폴더 경로를 먼저 정확히 지정해 주세요.");
                        return true;
                    }

                    if (!File.Exists(targetIni))
                    {
                        PopupService.ShowWarning("안내", "Engine.ini 파일이 없습니다.\n먼저 원하는 사양 프리셋을 적용한 뒤 다시 게임 실행을 눌러 주세요.");
                        return true;
                    }

                    EngineIniService.CleanupBeforeLaunch(targetIni);
                    var psi = new ProcessStartInfo
                    {
                        FileName = gameExe,
                        Arguments = EngineIniService.BuildLaunchArgs(Path.GetFileName(targetIni)),
                        WorkingDirectory = Path.GetDirectoryName(gameExe) ?? AppPaths.AppBaseDir,
                        UseShellExecute = true
                    };
                    Process.Start(psi);

                    AutoCloseNotice.Show("실행", $"게임 실행을 요청했습니다.\n\n실행 파일:\n{gameExe}", 2000);
                }
                catch (UnauthorizedAccessException)
                {
                    PopupService.ShowError(
                        "Engine.ini 쓰기 실패",
                        "게임 실행 전 Engine.ini를 수정하지 못했습니다.\n\n"
                        + $"대상 파일:\n{targetIni}\n\n"
                        + "파일이 읽기 전용이거나 다른 프로세스가 사용 중인지 확인해 주세요.");
                }
                catch (Exception ex)
                {
                    PopupService.ShowError("오류", $"게임 실행 중 문제가 발생했습니다.\n\n{ex.Message}");
                }

                return true;
            }

            if (args.Length >= 2 && (args[0] == "--elevated-clear-cache" || args[0] == "--elevated-delete-pso"))
            {
                var savedDir = args[1];
                var msg = EngineIniService.DeleteCacheFolders(savedDir);
                PopupService.ShowInfo("완료", msg);
                PopupService.ShowCacheRebuildNotice();
                return true;
            }

            if (args.Length >= 2 && args[0] == "--elevated-delete-ini")
            {
                var targetIni = args[1];
                if (!File.Exists(targetIni))
                {
                    PopupService.ShowWarning("안내", "삭제할 Engine.ini 파일이 없습니다.\n경로를 확인해 주세요.");
                    return true;
                }

                File.Delete(targetIni);
                PopupService.ShowInfo("완료", $"Engine.ini를 삭제했습니다.\n\n{targetIni}");
                return true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogDetail("elevated_mode_error", new Dictionary<string, string?>
            {
                ["args"] = string.Join(" ", args),
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString()
            });
            PopupService.ShowError("오류", $"관리자 모드 처리 중 문제가 발생했습니다.\n\n{ex.Message}");
            return true;
        }

        return false;
    }

    private static string ResolveSavedDir(string targetDir)
    {
        if (AppPaths.IsValidCustomConfigFolder(targetDir))
        {
            return Directory.GetParent(Directory.GetParent(targetDir)?.FullName ?? targetDir)?.FullName ?? targetDir;
        }

        return Path.Combine(Directory.GetParent(targetDir)?.Parent?.FullName ?? targetDir, "Saved");
    }
}
