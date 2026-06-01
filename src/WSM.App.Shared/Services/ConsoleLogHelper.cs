using System.Windows;

namespace WSM.App.Shared.Services;

/// <summary>
/// 控制台日志复制。
/// </summary>
public sealed class ConsoleLogHelper
{
    private readonly ISnackbarService _snackbarService;

    public ConsoleLogHelper(ISnackbarService snackbarService)
    {
        _snackbarService = snackbarService;
    }

    public void CopyToClipboard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _snackbarService.ShowWarning("没有可复制的内容。");
            return;
        }

        Clipboard.SetText(text);
        _snackbarService.ShowSuccess("已复制到剪贴板。");
    }
}
