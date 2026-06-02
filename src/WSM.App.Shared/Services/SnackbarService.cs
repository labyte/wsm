using MaterialDesignThemes.Wpf;
using WSM.Core.Interfaces;
using WSM.Core.Models;

namespace WSM.App.Shared.Services;

/// <summary>
/// 基于 Material Design Snackbar 的消息提示实现，并同步写入综合操作日志（operations.log）。
/// </summary>
public sealed class SnackbarService : ISnackbarService
{
    private const string UiLogCategory = "界面";

    private readonly SnackbarMessageQueue _messageQueue;
    private readonly IOperationLogSink _operationLog;

    public SnackbarService(SnackbarMessageQueue messageQueue, IOperationLogSink operationLog)
    {
        _messageQueue = messageQueue;
        _operationLog = operationLog;
    }

    public void ShowSuccess(string message) => Show(OperationLogLevel.Success, message);

    public void ShowError(string message) => Show(OperationLogLevel.Error, message);

    public void ShowWarning(string message) => Show(OperationLogLevel.Warning, message);

    public void ShowInfo(string message) => Show(OperationLogLevel.Info, message);

    private void Show(OperationLogLevel level, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _operationLog.Log(level, UiLogCategory, message);
        _messageQueue.Enqueue(message);
    }
}
