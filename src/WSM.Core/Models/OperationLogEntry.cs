using System;

namespace WSM.Core.Models;

/// <summary>
/// WSM 应用操作日志条目。
/// </summary>
public sealed class OperationLogEntry
{
    public DateTime TimestampLocal { get; set; }

    public OperationLogLevel Level { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string DisplayText =>
        $"[{TimestampLocal:HH:mm:ss}] [{LevelDisplay}] {Category}: {Message}";

    public string LevelDisplay => Level switch
    {
        OperationLogLevel.Success => "成功",
        OperationLogLevel.Warning => "警告",
        OperationLogLevel.Error => "错误",
        _ => "信息"
    };
}
