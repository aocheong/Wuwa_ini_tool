using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WuwaIniToolCs;

internal static class CacheService
{
    public static void EnsureCacheFile()
    {
        try
        {
            if (File.Exists(AppPaths.AppCachePath))
            {
                return;
            }

            File.WriteAllText(AppPaths.AppCachePath, "{}\n");
        }
        catch
        {
        }
    }

    public static Dictionary<string, JsonElement> LoadRaw()
    {
        try
        {
            if (!File.Exists(AppPaths.AppCachePath))
            {
                return new Dictionary<string, JsonElement>();
            }

            var raw = File.ReadAllText(AppPaths.AppCachePath).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Dictionary<string, JsonElement>();
            }

            var doc = JsonDocument.Parse(raw);
            var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                dict[property.Name] = property.Value.Clone();
            }

            return dict;
        }
        catch
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    public static void SaveValue<T>(string key, T value)
    {
        try
        {
            var current = LoadRaw();
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
            current[key] = doc.RootElement.Clone();
            SaveRaw(current);
        }
        catch
        {
        }
    }

    public static void SaveRaw(Dictionary<string, JsonElement> data)
    {
        try
        {
            var plain = new Dictionary<string, object?>();
            foreach (var kv in data)
            {
                plain[kv.Key] = JsonSerializer.Deserialize<object>(kv.Value.GetRawText());
            }

            var json = JsonSerializer.Serialize(plain, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(AppPaths.AppCachePath, json + "\n");
        }
        catch
        {
        }
    }

    public static void SaveGameExePath(string gameExePath)
    {
        SaveValue(AppConstants.CacheKeyGameExePath, gameExePath);
    }

    public static void SaveLastTargetDir(string targetDir)
    {
        SaveValue(AppConstants.CacheKeyLastTargetDir, targetDir);
    }

    public static string? GetCachedLastTargetDir()
    {
        try
        {
            var data = LoadRaw();
            if (data.TryGetValue(AppConstants.CacheKeyLastTargetDir, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var cached = value.GetString();
                if (!string.IsNullOrWhiteSpace(cached) && AppPaths.IsValidTargetIniFolder(cached))
                {
                    return cached;
                }
            }

            var cachedExe = GetCachedGameExePath();
            if (!string.IsNullOrWhiteSpace(cachedExe))
            {
                var cachedWin64 = AppPaths.GameExeToWin64(cachedExe);
                if (!string.IsNullOrWhiteSpace(cachedWin64) && AppPaths.IsValidTargetIniFolder(cachedWin64))
                {
                    SaveLastTargetDir(cachedWin64);
                    return cachedWin64;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    public static string? GetCachedGameExePath()
    {
        try
        {
            var data = LoadRaw();
            if (data.TryGetValue(AppConstants.CacheKeyGameExePath, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var cached = value.GetString();
                if (!string.IsNullOrWhiteSpace(cached) && File.Exists(cached))
                {
                    return cached;
                }
            }

            if (File.Exists(AppPaths.LegacyGameExeCachePath))
            {
                var legacy = File.ReadAllText(AppPaths.LegacyGameExeCachePath).Trim();
                if (!string.IsNullOrWhiteSpace(legacy) && File.Exists(legacy))
                {
                    SaveGameExePath(legacy);
                    var legacyWin64 = AppPaths.GameExeToWin64(legacy);
                    if (!string.IsNullOrWhiteSpace(legacyWin64) && AppPaths.IsValidTargetIniFolder(legacyWin64))
                    {
                        SaveLastTargetDir(legacyWin64);
                    }
                    return legacy;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    public static void SaveRtEnabled(bool enabled)
    {
        SaveValue(AppConstants.CacheKeyRtEnabled, enabled);
    }

    public static bool GetCachedRtEnabled()
    {
        try
        {
            var data = LoadRaw();
            if (data.TryGetValue(AppConstants.CacheKeyRtEnabled, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => value.GetInt32() != 0,
                    JsonValueKind.String => ParseBoolString(value.GetString()),
                    _ => false
                };
            }

            if (File.Exists(AppPaths.LegacyRtCachePath))
            {
                var legacy = File.ReadAllText(AppPaths.LegacyRtCachePath).Trim();
                var migrated = legacy == "1";
                SaveRtEnabled(migrated);
                return migrated;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool ParseBoolString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            _ => false
        };
    }
}
