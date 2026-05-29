namespace WSM.Core.Models;

/// <summary>
/// 日志查询参数。
/// </summary>
public sealed class LogQuery
{
    public LogFilter Filter { get; set; } = new LogFilter();

    public int Skip { get; set; }

    public int Take { get; set; } = 500;
}
