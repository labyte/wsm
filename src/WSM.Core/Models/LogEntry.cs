using System;

namespace WSM.Core.Models;

/// <summary>
/// 单条日志记录。
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; set; }

    public string ServiceId { get; set; } = string.Empty;

    public LogLevel Level { get; set; } = LogLevel.Unknown;

    public LogSource Source { get; set; } = LogSource.StdOut;

    public string Message { get; set; } = string.Empty;

    public long FileOffset { get; set; }

    public string SourceFile { get; set; } = string.Empty;
}
