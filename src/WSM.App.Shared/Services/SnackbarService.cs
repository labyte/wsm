using MaterialDesignThemes.Wpf;

namespace WSM.App.Shared.Services;

/// <summary>
/// 基于 Material Design Snackbar 的消息提示实现。
/// </summary>
public sealed class SnackbarService : ISnackbarService
{
    private readonly SnackbarMessageQueue _messageQueue;

    public SnackbarService(SnackbarMessageQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    public void ShowSuccess(string message) => Enqueue(message);

    public void ShowError(string message) => Enqueue(message);

    public void ShowWarning(string message) => Enqueue(message);

    public void ShowInfo(string message) => Enqueue(message);

    private void Enqueue(string message)
    {
        _messageQueue.Enqueue(message);
    }
}
