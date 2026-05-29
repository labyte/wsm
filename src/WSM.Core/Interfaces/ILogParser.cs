using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// 日志级别解析器。
/// </summary>
public interface ILogParser
{
    LogLevel ParseLevel(string line, LogSource source);
}
