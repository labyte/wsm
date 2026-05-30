using System;
using WSM.Core.Interfaces;
using WSM.Core.Models;

namespace WSM.App.Shared.Services;

/// <summary>
/// 聚合多个操作日志写入端，统一分发日志。
/// </summary>
public sealed class CompositeOperationLogSink : IOperationLogSink
{
    private readonly IOperationLogSink[] _sinks;

    public CompositeOperationLogSink(params IOperationLogSink[] sinks)
    {
        _sinks = sinks ?? Array.Empty<IOperationLogSink>();
    }

    public void Log(OperationLogLevel level, string category, string message)
    {
        foreach (var sink in _sinks)
        {
            sink.Log(level, category, message);
        }
    }
}
