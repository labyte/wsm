namespace WSM.Core.Models;

/// <summary>
/// WinSW 日志策略。
/// </summary>
public sealed class LogPolicy
{
    public LogMode Mode { get; set; } = LogMode.RollBySize;

    /// <summary>
    /// 单文件大小上限（KB）。
    /// </summary>
    public int SizeThresholdKb { get; set; } = 10240;

    public int KeepFiles { get; set; } = 10;

    public static LogPolicy CreateDefault()
    {
        return new LogPolicy();
    }
}
