namespace WSM.Core.Models;

/// <summary>
/// WinSW 日志模式。
/// </summary>
public enum LogMode
{
    Append,
    Reset,
    Ignore,
    Roll,
    RollBySize,
    RollByTime,
    RollBySizeTime
}
