using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Infrastructure.Paths;

namespace WSM.Infrastructure.Logging;

/// <summary>
/// 基于 NLog 的操作日志落盘实现（按日文件 operations_yyyy-MM-dd.log）。
/// </summary>
public sealed class NLogOperationLogSink : IOperationLogSink
{
    private const string LoggerName = "WSM.Operation";
    private static readonly object ConfigLock = new();
    private static bool _configured;
    private static string _configuredKey = string.Empty;

    private readonly WsmPaths _paths;
    private readonly Logger _logger;

    public NLogOperationLogSink(WsmPaths paths)
    {
        _paths = paths;
        EnsureLogFilePathReady();
        _logger = LogManager.GetLogger(LoggerName);
    }

    public void Log(OperationLogLevel level, string category, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureLogFilePathReady();

        var logEvent = new LogEventInfo
        {
            LoggerName = LoggerName,
            Level = MapLogLevel(level),
            Message = message
        };
        logEvent.Properties["operationLevel"] = level.ToString();
        logEvent.Properties["category"] = category ?? string.Empty;

        _logger.Log(logEvent);
    }

    private void EnsureLogFilePathReady()
    {
        var logFilePath = _paths.OperationLogPath;
        var directory = OperationLogPathHelper.GetLogDirectory(logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        EnsureNLogConfigured(logFilePath);
    }

    private static void EnsureNLogConfigured(string operationLogPath)
    {
        var configKey = OperationLogPathHelper.GetLogDirectory(operationLogPath)
                        + "|"
                        + OperationLogPathHelper.GetBaseName(operationLogPath);

        lock (ConfigLock)
        {
            if (_configured && string.Equals(_configuredKey, configKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var configuration = new LoggingConfiguration();
            var fileTarget = new FileTarget("operation-file")
            {
                FileName = OperationLogPathHelper.BuildNLogDailyFileNameLayout(operationLogPath),
                Layout = "${longdate}|${event-properties:item=operationLevel}|${event-properties:item=category}|${message}",
                KeepFileOpen = false,
                Encoding = System.Text.Encoding.UTF8
            };

            configuration.AddTarget(fileTarget);
            configuration.AddRuleForAllLevels(fileTarget, LoggerName);

            LogManager.Configuration = configuration;
            _configured = true;
            _configuredKey = configKey;
        }
    }

    private static NLog.LogLevel MapLogLevel(OperationLogLevel level)
    {
        return level switch
        {
            OperationLogLevel.Warning => NLog.LogLevel.Warn,
            OperationLogLevel.Error => NLog.LogLevel.Error,
            _ => NLog.LogLevel.Info
        };
    }
}
