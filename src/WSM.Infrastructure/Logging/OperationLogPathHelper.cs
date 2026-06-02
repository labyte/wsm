using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WSM.Infrastructure.Logging;

/// <summary>
/// 操作日志路径约定：按日文件名为 {baseName}_yyyy-MM-dd.log（如 operations_2026-06-02.log）。
/// </summary>
public static class OperationLogPathHelper
{
    public const string DateFormat = "yyyy-MM-dd";

    private static readonly Regex DatedFilePattern = new Regex(
        @"^(?<base>.+?)_(?<date>\d{4}-\d{2}-\d{2})\.log$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string GetBaseName(string operationLogPath)
    {
        var baseName = Path.GetFileNameWithoutExtension(operationLogPath);
        return string.IsNullOrWhiteSpace(baseName) ? "operations" : baseName;
    }

    public static string GetLogDirectory(string operationLogPath)
        => Path.GetDirectoryName(operationLogPath) ?? string.Empty;

    public static string BuildDailyLogFilePath(string operationLogPath, DateTime date)
    {
        var directory = GetLogDirectory(operationLogPath);
        var baseName = GetBaseName(operationLogPath);
        var fileName = $"{baseName}_{date.ToString(DateFormat, CultureInfo.InvariantCulture)}.log";
        return string.IsNullOrWhiteSpace(directory) ? fileName : Path.Combine(directory, fileName);
    }

    public static string BuildNLogDailyFileNameLayout(string operationLogPath)
    {
        var directory = GetLogDirectory(operationLogPath);
        var baseName = GetBaseName(operationLogPath);
        var pattern = $"{baseName}_${{date:format=yyyy-MM-dd}}.log";
        return string.IsNullOrWhiteSpace(directory) ? pattern : Path.Combine(directory, pattern);
    }

    /// <summary>
    /// 读取用：优先当天文件；不存在则取目录内最后改动的操作日志文件。
    /// </summary>
    public static string ResolveFileForRead(string operationLogPath, DateTime? referenceTime = null)
    {
        var todayPath = BuildDailyLogFilePath(operationLogPath, (referenceTime ?? DateTime.Now).Date);
        if (File.Exists(todayPath))
        {
            return todayPath;
        }

        var candidates = DiscoverAllOperationLogFiles(operationLogPath);
        if (candidates.Count == 0)
        {
            return todayPath;
        }

        return candidates
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ThenByDescending(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .First()
            .FullName;
    }

    /// <summary>
    /// 发现目录下所有操作日志文件（含按日文件与旧版滚动文件）。
    /// </summary>
    public static IReadOnlyList<string> DiscoverAllOperationLogFiles(string operationLogPath)
    {
        if (string.IsNullOrWhiteSpace(operationLogPath))
        {
            return Array.Empty<string>();
        }

        var directory = GetLogDirectory(operationLogPath);
        var baseName = GetBaseName(operationLogPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return File.Exists(operationLogPath) ? new[] { operationLogPath } : Array.Empty<string>();
        }

        return Directory.GetFiles(directory, "*.log", SearchOption.TopDirectoryOnly)
            .Where(path => IsOperationLogFileName(Path.GetFileName(path), baseName))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsOperationLogFileName(string fileName, string baseName)
    {
        if (!fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(fileName, baseName + ".log", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (DatedFilePattern.IsMatch(fileName)
            && string.Equals(DatedFilePattern.Match(fileName).Groups["base"].Value, baseName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileName.StartsWith(baseName + "_", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase);
    }

    public static DateTime? TryParseDateFromFileName(string fileName, string baseName)
    {
        var match = DatedFilePattern.Match(fileName);
        if (!match.Success
            || !string.Equals(match.Groups["base"].Value, baseName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return DateTime.TryParseExact(
            match.Groups["date"].Value,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }
}
