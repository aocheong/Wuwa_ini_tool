using System.Collections.Generic;

namespace WuwaIniToolCs;

internal static class AppConstants
{
    public const string GameExeName = "Client-Win64-Shipping.exe";
    public const string AppCacheFile = "app_cache.json";
    public const string LegacyGameExeCacheFile = "run_wuwa_cache.txt";
    public const string LegacyRtCacheFile = "rt_state_cache.txt";
    public const string PermissionDebugLogFile = "permission_debug_log.txt";
    public const string CacheKeyGameExePath = "game_exe_path";
    public const string CacheKeyLastTargetDir = "last_target_dir";
    public const string CacheKeyRtEnabled = "rt_enabled";
    public const int MaxBackupCount = 10;

    public static readonly string[] CommonGameLaunchArgs =
    {
        "-SkipSplash",
        "-NoVerifyGC",
        "-USEALLAVAILABLECORES"
    };

    public static readonly Dictionary<string, string> PresetRecommendedSpecs = new()
    {
        ["ultra"] = "CPU: 7800X3D / GPU: RTX 5080 동급 이상 권장",
        ["high"] = "CPU: 9700X / GPU: RTX 5070 또는 RX 9070 동급 이상 권장",
        ["mid_high"] = "CPU: 9600X / GPU: RTX 5060 또는 RX 9060 동급 이상 권장",
        ["mid_low"] = "CPU: 7500F / GPU: RTX 3060 또는 RX 7600 동급 이상 권장",
        ["low"] = "CPU: 5600 / GPU: GTX 1660 또는 RX 6500 XT 동급 이상 권장",
        ["very_low"] = "GPU: GTX 1060 3GB 이하급 그래픽 권장"
    };

    public static readonly string[] PresetKeysOff =
    {
        "ultra", "high", "mid_high", "mid_low", "low", "very_low"
    };

    public static readonly string[] PresetKeysOn =
    {
        "ultra", "high", "mid_high", "mid_low"
    };
}
