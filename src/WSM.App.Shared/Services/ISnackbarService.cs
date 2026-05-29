namespace WSM.App.Shared.Services;

/// <summary>
/// 冒泡式消息提示服务（Snackbar），禁止使用 MessageBox。
/// </summary>
public interface ISnackbarService
{
    void ShowSuccess(string message);

    void ShowError(string message);

    void ShowWarning(string message);

    void ShowInfo(string message);
}
