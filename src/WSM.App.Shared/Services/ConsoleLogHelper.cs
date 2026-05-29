using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace WSM.App.Shared.Services;

/// <summary>
/// 控制台日志复制与导出。
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

    public void ExportToFile(string? text, string defaultFileName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _snackbarService.ShowWarning("没有可导出的内容。");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = defaultFileName,
            DefaultExt = ".log"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _snackbarService.ShowSuccess($"已导出到 {dialog.FileName}");
    }
}
