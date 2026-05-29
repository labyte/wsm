using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// WSM 操作日志写入端。
/// </summary>
public interface IOperationLogSink
{
    void Log(OperationLogLevel level, string category, string message);
}
