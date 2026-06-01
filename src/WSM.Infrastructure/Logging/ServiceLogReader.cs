using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WSM.Infrastructure.Paths;

namespace WSM.Infrastructure.Logging;

/// <summary>
/// 托管服务日志行。
/// </summary>
public sealed class ServiceLogLine
{
    public string ServiceId { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime? Timestamp { get; set; }

    public string DisplayText
    {
        get
        {
            var prefix = string.IsNullOrEmpty(ServiceId) ? string.Empty : $"[{ServiceId}] ";
            if (Timestamp.HasValue)
            {
                return $"[{Timestamp.Value:yyyy-MM-dd HH:mm:ss}] {prefix}{Text}";
            }

            return $"{prefix}{Text}";
        }
    }
}

/// <summary>
/// 日志清理失败项。
/// </summary>
public sealed class LogClearFailure
{
    public string FilePath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 日志清理结果。
/// </summary>
public sealed class LogClearResult
{
    public int ClearedCount { get; set; }
    public List<LogClearFailure> Failures { get; } = new List<LogClearFailure>();
}

/// <summary>
/// 读取 WinSW 服务日志文件（wrapper / out / err）。
/// </summary>
public sealed class ServiceLogReader
{
    private static readonly Regex TimestampPattern = new Regex(
        @"^(?<ts>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(?<msg>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly WsmPaths _paths;

    public ServiceLogReader(WsmPaths paths)
    {
        _paths = paths;
    }

    public IReadOnlyList<ServiceLogLine> ReadMergedLogs(
        IReadOnlyList<string> serviceIds,
        int maxLines = 3000)
    {
        return ReadMergedLogs(serviceIds, null, maxLines);
    }

    public IReadOnlyList<ServiceLogLine> ReadMergedLogs(
        IReadOnlyList<string> serviceIds,
        IReadOnlyDictionary<string, string>? externalLogFiles,
        int maxLines = 3000)
    {
        var lines = new List<ServiceLogLine>();

        foreach (var serviceId in serviceIds)
        {
            foreach (var file in DiscoverLogFiles(serviceId))
            {
                lines.AddRange(ReadLogFile(serviceId, file));
            }

            if (externalLogFiles != null
                && externalLogFiles.TryGetValue(serviceId, out var externalFile)
                && !string.IsNullOrWhiteSpace(externalFile))
            {
                lines.AddRange(ReadLogFile(serviceId, externalFile, "external"));
            }
        }

        // 按文件读取顺序与文件内原始行序输出，不做二次排序。
        return TakeLastSafe(lines, maxLines);
    }

    /// <summary>
    /// 按目录与扩展名规则解析最新改动的日志文件。
    /// </summary>
    public string? ResolveLatestExternalLogFile(string? directoryPath, string? extensionFilterText)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        var normalizedDirectory = (directoryPath ?? string.Empty).Trim();
        if (!Directory.Exists(normalizedDirectory))
        {
            return null;
        }

        var allowedExtensions = ParseExtensions(extensionFilterText);
        var candidates = Directory.GetFiles(normalizedDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                if (allowedExtensions.Count == 0)
                {
                    return true;
                }

                var extension = Path.GetExtension(path) ?? string.Empty;
                return allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
            })
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ThenBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates.Count > 0 ? candidates[0].FullName : null;
    }

    /// <summary>
    /// 读取 WSM 操作日志文件（operations.log，保持文件内原始顺序）。
    /// </summary>
    public IReadOnlyList<ServiceLogLine> ReadOperationLog(string operationLogPath, int maxLines = 3000)
    {
        if (string.IsNullOrWhiteSpace(operationLogPath))
        {
            return Array.Empty<ServiceLogLine>();
        }

        var lines = ReadLogFile(string.Empty, operationLogPath, "operations").ToList();
        return TakeLastSafe(lines, maxLines);
    }

    /// <summary>
    /// 读取指定服务的 wrapper 历史日志（保持文件内原始顺序）。
    /// </summary>
    public IReadOnlyList<ServiceLogLine> ReadMergedWrapperLogs(
        IReadOnlyList<string> serviceIds,
        int maxLines = 3000)
    {
        var lines = new List<ServiceLogLine>();

        foreach (var serviceId in serviceIds)
        {
            foreach (var file in DiscoverWrapperLogFiles(serviceId))
            {
                lines.AddRange(ReadLogFile(serviceId, file));
            }
        }

        return TakeLastSafe(lines, maxLines);
    }

    private static List<ServiceLogLine> TakeLastSafe(List<ServiceLogLine> ordered, int maxLines)
    {
        if (ordered.Count <= maxLines)
        {
            return ordered;
        }

        return ordered.Skip(ordered.Count - maxLines).ToList();
    }

    public IReadOnlyList<string> DiscoverLogFiles(string serviceId)
    {
        return DiscoverLogFilesInternal(serviceId, includeWrapperLogs: false);
    }

    /// <summary>
    /// 发现指定服务的 wrapper 日志文件（*.wrapper.log）。
    /// </summary>
    public IReadOnlyList<string> DiscoverWrapperLogFiles(string serviceId)
    {
        return DiscoverLogFilesInternal(serviceId, includeWrapperLogs: true)
            .Where(IsWrapperLogFile)
            .ToList();
    }

    private IReadOnlyList<string> DiscoverLogFilesInternal(string serviceId, bool includeWrapperLogs)
    {
        var results = new List<string>();
        var serviceDirectory = _paths.GetServiceDirectory(serviceId);
        var logsDirectory = _paths.GetServiceLogsDirectory(serviceId);

        AddIfExists(results, Path.Combine(serviceDirectory, serviceId + ".out.log"));
        AddIfExists(results, Path.Combine(serviceDirectory, serviceId + ".err.log"));

        if (Directory.Exists(logsDirectory))
        {
            results.AddRange(Directory.GetFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly));
        }

        if (Directory.Exists(serviceDirectory))
        {
            results.AddRange(Directory.GetFiles(serviceDirectory, serviceId + ".*.log", SearchOption.TopDirectoryOnly));
        }

        var filtered = includeWrapperLogs
            ? results.Where(path => IsWrapperLogFile(path))
            : results.Where(path => !IsWrapperLogFile(path));

        return filtered
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> DiscoverServiceIdsWithLogs()
    {
        if (!Directory.Exists(_paths.ServicesDirectory))
        {
            return Array.Empty<string>();
        }

        var serviceIds = new List<string>();
        foreach (var directory in Directory.GetDirectories(_paths.ServicesDirectory))
        {
            var serviceId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                continue;
            }

            var hasRootLogs = Directory.GetFiles(directory, "*.log", SearchOption.TopDirectoryOnly).Length > 0;
            var logsDirectory = Path.Combine(directory, "logs");
            var hasNestedLogs = Directory.Exists(logsDirectory)
                                && Directory.GetFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly).Length > 0;
            if (hasRootLogs || hasNestedLogs)
            {
                serviceIds.Add(serviceId);
            }
        }

        return serviceIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 发现存在 wrapper 日志的服务。
    /// </summary>
    public IReadOnlyList<string> DiscoverServiceIdsWithWrapperLogs()
    {
        if (!Directory.Exists(_paths.ServicesDirectory))
        {
            return Array.Empty<string>();
        }

        var serviceIds = new List<string>();
        foreach (var directory in Directory.GetDirectories(_paths.ServicesDirectory))
        {
            var serviceId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                continue;
            }

            if (DiscoverWrapperLogFiles(serviceId).Count > 0)
            {
                serviceIds.Add(serviceId);
            }
        }

        return serviceIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public int ClearLogs(IReadOnlyList<string> serviceIds)
    {
        return ClearLogsDetailed(serviceIds).ClearedCount;
    }

    /// <summary>
    /// 清空所有服务的 wrapper 日志文件内容。
    /// </summary>
    public int ClearWrapperLogs(IReadOnlyList<string> serviceIds)
    {
        return ClearWrapperLogsDetailed(serviceIds).ClearedCount;
    }

    /// <summary>
    /// 清空指定日志文件集合内容。
    /// </summary>
    public int ClearLogFiles(IEnumerable<string> filePaths)
    {
        return ClearLogFilesDetailed(filePaths).ClearedCount;
    }

    /// <summary>
    /// 清空受管服务日志并返回明细结果。
    /// </summary>
    public LogClearResult ClearLogsDetailed(IReadOnlyList<string> serviceIds)
    {
        var result = new LogClearResult();
        foreach (var serviceId in serviceIds)
        {
            var partial = ClearLogFilesDetailed(DiscoverLogFiles(serviceId));
            result.ClearedCount += partial.ClearedCount;
            result.Failures.AddRange(partial.Failures);
        }

        return result;
    }

    /// <summary>
    /// 清空 wrapper 日志并返回明细结果。
    /// </summary>
    public LogClearResult ClearWrapperLogsDetailed(IReadOnlyList<string> serviceIds)
    {
        var result = new LogClearResult();
        foreach (var serviceId in serviceIds)
        {
            var partial = ClearLogFilesDetailed(DiscoverWrapperLogFiles(serviceId));
            result.ClearedCount += partial.ClearedCount;
            result.Failures.AddRange(partial.Failures);
        }

        return result;
    }

    /// <summary>
    /// 清空指定日志文件集合并返回明细结果。
    /// </summary>
    public LogClearResult ClearLogFilesDetailed(IEnumerable<string> filePaths)
    {
        var result = new LogClearResult();
        foreach (var filePath in filePaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    continue;
                }

                // 允许并发写入场景下尽量完成截断，提升“清空日志”成功率。
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                stream.SetLength(0);
                result.ClearedCount++;
            }
            catch (IOException)
            {
                result.Failures.Add(new LogClearFailure
                {
                    FilePath = filePath,
                    Reason = "文件被占用"
                });
            }
            catch (UnauthorizedAccessException)
            {
                result.Failures.Add(new LogClearFailure
                {
                    FilePath = filePath,
                    Reason = "权限不足"
                });
            }
        }

        return result;
    }

    private IEnumerable<ServiceLogLine> ReadLogFile(string serviceId, string filePath, string? sourceHint = null)
    {
        if (!File.Exists(filePath))
        {
            yield break;
        }

        string source = "log";
        if (!string.IsNullOrWhiteSpace(sourceHint))
        {
            source = sourceHint ?? "log";
        }
        else
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.IndexOf(".wrapper.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                source = "wrapper";
            }
            else if (fileName.IndexOf(".out.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                source = "stdout";
            }
            else if (fileName.IndexOf(".err.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                source = "stderr";
            }
        }

        foreach (var rawLine in ReadLogLinesSafe(filePath))
        {
            var normalizedLine = rawLine ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedLine))
            {
                continue;
            }

            var line = new ServiceLogLine
            {
                ServiceId = serviceId,
                Source = source ?? "log",
                Text = normalizedLine
            };

            var match = TimestampPattern.Match(normalizedLine);
            if (match.Success
                && DateTime.TryParse(match.Groups["ts"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ts))
            {
                line.Timestamp = ts;
                line.Text = match.Groups["msg"].Value ?? string.Empty;
            }

            yield return line;
        }
    }

    private static IEnumerable<string> ReadLogLinesSafe(string filePath)
    {
        try
        {
            // 允许在日志被服务进程写入时并发读取，减少“文件占用”导致的整批读取失败。
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                var text = LogTextEncodingHelper.DecodeBytes(memory.ToArray());
                return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            }
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static void AddIfExists(ICollection<string> list, string path)
    {
        if (File.Exists(path))
        {
            list.Add(path);
        }
    }

    private static bool IsWrapperLogFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.IndexOf(".wrapper.", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static List<string> ParseExtensions(string? extensionFilterText)
    {
        if (string.IsNullOrWhiteSpace(extensionFilterText))
        {
            return new List<string> { ".log", ".txt" };
        }

        var normalizedFilter = extensionFilterText ?? string.Empty;
        return normalizedFilter
            .Split(new[] { ';', ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.StartsWith(".") ? x : "." + x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
