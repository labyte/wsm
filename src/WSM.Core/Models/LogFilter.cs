using System;
using System.Collections.Generic;

namespace WSM.Core.Models;

/// <summary>
/// 日志筛选条件。
/// </summary>
public sealed class LogFilter
{
    public string? ServiceId { get; set; }

    public LogLevel? MinimumLevel { get; set; }

    public string? Keyword { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public IReadOnlyList<LogSource>? Sources { get; set; }
}
