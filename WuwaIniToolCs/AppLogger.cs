using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WuwaIniToolCs;

internal static class AppLogger
{
    public static void LogDetail(string eventName, Dictionary<string, string?> fields)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append('[')
              .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
              .Append("] ")
              .AppendLine(eventName);

            foreach (var pair in fields)
            {
                sb.Append(pair.Key).Append(": ").AppendLine(pair.Value ?? string.Empty);
            }

            sb.AppendLine();
            File.AppendAllText(AppPaths.PermissionDebugLogPath, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
    }
}
