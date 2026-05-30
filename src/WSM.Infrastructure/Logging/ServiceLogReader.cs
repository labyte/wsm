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
        var lines = new List<ServiceLogLine>();

        foreach (var serviceId in serviceIds)
        {
            foreach (var file in DiscoverLogFiles(serviceId))
            {
                lines.AddRange(ReadLogFile(serviceId, file));
            }
        }

        var ordered = lines
            .OrderBy(x => x.Timestamp ?? DateTime.MinValue)
            .ThenBy(x => x.Text, StringComparer.Ordinal)
            .ToList();

        return TakeLastSafe(ordered, maxLines);
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

        return results
            .Where(path => !IsWrapperLogFile(path))
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

    public int ClearLogs(IReadOnlyList<string> serviceIds)
    {
        var clearedCount = 0;
        foreach (var serviceId in serviceIds)
        {
            foreach (var filePath in DiscoverLogFiles(serviceId))
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    File.WriteAllText(filePath, string.Empty);
                    clearedCount++;
                }
                catch (IOException)
                {
                    // 忽略单个文件清理失败，继续处理其余日志文件。
                }
                catch (UnauthorizedAccessException)
                {
                    // 忽略权限不足文件，避免中断整个清理流程。
                }
            }
        }

        return clearedCount;
    }

    private IEnumerable<ServiceLogLine> ReadLogFile(string serviceId, string filePath)
    {
        if (!File.Exists(filePath))
        {
            yield break;
        }

        string source;
        var fileName = Path.GetFileName(filePath);
        if (fileName.Contains(".wrapper.", StringComparison.OrdinalIgnoreCase))
        {
            source = "wrapper";
        }
        else if (fileName.Contains(".out.", StringComparison.OrdinalIgnoreCase))
        {
            source = "stdout";
        }
        else if (fileName.Contains(".err.", StringComparison.OrdinalIgnoreCase))
        {
            source = "stderr";
        }
        else
        {
            source = "log";
        }

        foreach (var rawLine in ReadLogLinesSafe(filePath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var line = new ServiceLogLine
            {
                ServiceId = serviceId,
                Source = source,
                Text = rawLine
            };

            var match = TimestampPattern.Match(rawLine);
            if (match.Success
                && DateTime.TryParse(match.Groups["ts"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ts))
            {
                line.Timestamp = ts;
                line.Text = match.Groups["msg"].Value;
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
}
