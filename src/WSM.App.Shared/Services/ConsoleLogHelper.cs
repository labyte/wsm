using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WSM.App.Shared.Services;

/// <summary>
/// 控制台日志复制。
/// </summary>
public sealed class ConsoleLogHelper
{
    /// <summary>剪贴板文本上限（字符），避免超大日志导致 OOM 或系统剪贴板失败。</summary>
    private const int MaxClipboardCharacters = 2_000_000;

    private const int ClipboardRetryCount = 8;

    private readonly ISnackbarService _snackbarService;

    public ConsoleLogHelper(ISnackbarService snackbarService)
    {
        _snackbarService = snackbarService;
    }

    /// <summary>异步复制到剪贴板，避免在 UI 线程阻塞导致界面卡死。</summary>
    public async Task CopyToClipboardAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _snackbarService.ShowWarning("没有可复制的内容。");
            return;
        }

        if (text.Length > MaxClipboardCharacters)
        {
            _snackbarService.ShowWarning(
                $"日志内容过大（{text.Length:N0} 字符），请降低「最大条数」后再复制。");
            return;
        }

        var copied = await Task.Run(() => TrySetClipboardOnStaThread(text)).ConfigureAwait(true);
        if (copied)
        {
            _snackbarService.ShowSuccess("已复制到剪贴板。");
        }
        else
        {
            _snackbarService.ShowWarning("复制失败：剪贴板被占用或系统拒绝写入，请稍后重试。");
        }
    }

    /// <summary>
    /// 在独立 STA 线程写入剪贴板（WinForms API），不阻塞 WPF UI 线程。
    /// </summary>
    private static bool TrySetClipboardOnStaThread(string text)
    {
        var success = false;
        Exception? lastError = null;

        var thread = new Thread(() =>
        {
            for (var attempt = 0; attempt < ClipboardRetryCount; attempt++)
            {
                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    success = true;
                    return;
                }
                catch (Exception ex) when (ex is COMException or ExternalException)
                {
                    lastError = ex;
                    if (attempt < ClipboardRetryCount - 1)
                    {
                        Thread.Sleep(30 + attempt * 25);
                    }
                }
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));

        if (!success && lastError != null)
        {
            System.Diagnostics.Debug.WriteLine("Clipboard copy failed: " + lastError.Message);
        }

        return success;
    }
}
