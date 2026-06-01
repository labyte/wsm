using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using WSM.App.Shared.ViewModels;
using WSM.Core;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using Application = System.Windows.Application;

namespace WSM.App.Shared.Services;

/// <summary>
/// 基于 NotifyIcon 的托盘实现，兼容 Legacy（Win7）与 Modern。
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private readonly ISnackbarService _snackbarService;
    private readonly IServiceRepository _serviceRepository;
    private readonly IWinSwHostService _winSwHostService;
    private readonly ServiceListViewModel _serviceListViewModel;
    private readonly ICloseWindowPreferenceStore _closeWindowPreferenceStore;
    private NotifyIcon? _notifyIcon;
    private Window? _mainWindow;
    private bool _isExplicitExit;
    private bool _disposed;
    private bool _isBatchOperating;
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _startAllItem;
    private ToolStripMenuItem? _stopAllItem;
    private Icon? _trayIcon;

    public TrayIconService(
        ISnackbarService snackbarService,
        IServiceRepository serviceRepository,
        IWinSwHostService winSwHostService,
        ServiceListViewModel serviceListViewModel,
        ICloseWindowPreferenceStore closeWindowPreferenceStore)
    {
        _snackbarService = snackbarService;
        _serviceRepository = serviceRepository;
        _winSwHostService = winSwHostService;
        _serviceListViewModel = serviceListViewModel;
        _closeWindowPreferenceStore = closeWindowPreferenceStore;
        MinimizeOnClose = closeWindowPreferenceStore.LoadMinimizeOnClose() ?? true;
    }

    public bool MinimizeOnClose { get; set; }

    public void Attach(Window mainWindow)
    {
        if (_notifyIcon != null)
        {
            throw new InvalidOperationException("托盘已初始化。");
        }

        MinimizeOnClose = _closeWindowPreferenceStore.LoadMinimizeOnClose() ?? MinimizeOnClose;
        _mainWindow = mainWindow;
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.StateChanged += OnMainWindowStateChanged;

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon = LoadTrayIcon(),
            Text = WsmConstants.AppDisplayName,
            Visible = true
        };

        _notifyIcon.DoubleClick += OnTrayDoubleClick;
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        _ = RefreshBatchMenuStateAsync();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            return;
        }

        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.ShowInTaskbar = true;
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }

    public void HideToTray(bool showBalloon = true)
    {
        if (_mainWindow == null)
        {
            return;
        }

        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Hide();
            _mainWindow.ShowInTaskbar = false;
        });

        if (showBalloon && _notifyIcon != null)
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                WsmConstants.AppDisplayName,
                "程序已最小化到托盘，双击图标可恢复窗口。",
                ToolTipIcon.Info);
        }
    }

    public void RequestExit()
    {
        _isExplicitExit = true;
        _notifyIcon!.Visible = false;

        if (_mainWindow != null)
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.Close());
        }

        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_mainWindow != null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
            _mainWindow.StateChanged -= OnMainWindowStateChanged;
        }

        if (_notifyIcon != null)
        {
            _notifyIcon.DoubleClick -= OnTrayDoubleClick;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("显示主窗口", null, (_, _) => ShowMainWindow());
        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());

        _statusItem = new ToolStripMenuItem("服务状态：0/0")
        {
            Enabled = false
        };
        menu.Items.Add(_statusItem);

        _startAllItem = new ToolStripMenuItem("启动全部服务", null, async (_, _) => await StartAllServicesAsync().ConfigureAwait(false))
        {
            Enabled = false
        };
        _stopAllItem = new ToolStripMenuItem("停止全部服务", null, async (_, _) => await StopAllServicesAsync().ConfigureAwait(false))
        {
            Enabled = false
        };
        menu.Opening += async (_, _) => await RefreshBatchMenuStateAsync().ConfigureAwait(false);
        menu.Items.Add(_startAllItem);
        menu.Items.Add(_stopAllItem);
        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("退出", null, (_, _) => RequestExit());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExplicitExit || !MinimizeOnClose)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
        _snackbarService.ShowInfo("已最小化到系统托盘");
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow?.WindowState == WindowState.Minimized && MinimizeOnClose)
        {
            HideToTray(showBalloon: false);
        }
    }

    private async Task StartAllServicesAsync()
    {
        await ExecuteBatchLifecycleAsync(start: true).ConfigureAwait(false);
    }

    private async Task StopAllServicesAsync()
    {
        await ExecuteBatchLifecycleAsync(start: false).ConfigureAwait(false);
    }

    private async Task ExecuteBatchLifecycleAsync(bool start)
    {
        if (_isBatchOperating)
        {
            return;
        }

        _isBatchOperating = true;
        await RefreshBatchMenuStateAsync().ConfigureAwait(false);

        try
        {
            var services = await _serviceRepository.GetAllAsync().ConfigureAwait(false);
            if (services.Count == 0)
            {
                ShowInfoOnUi("当前没有已托管服务。");
                return;
            }

            var success = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var service in services.OrderBy(x => x.DisplayName))
            {
                var status = await _winSwHostService.GetStatusAsync(service.Id).ConfigureAwait(false);
                var canOperate = start
                    ? status != ServiceRuntimeStatus.Running && status != ServiceRuntimeStatus.StartPending
                    : status == ServiceRuntimeStatus.Running || status == ServiceRuntimeStatus.StartPending;

                if (!canOperate)
                {
                    skipped++;
                    continue;
                }

                var result = start
                    ? await _winSwHostService.StartAsync(service.Id).ConfigureAwait(false)
                    : await _winSwHostService.StopAsync(service.Id).ConfigureAwait(false);
                if (result.Success)
                {
                    success++;
                }
                else
                {
                    failed++;
                }
            }

            var actionText = start ? "启动" : "停止";
            if (failed == 0)
            {
                ShowTrayNotification(
                    $"{WsmConstants.AppDisplayName} - 托盘通知",
                    $"批量{actionText}完成：成功 {success}，跳过 {skipped}。",
                    ToolTipIcon.Info);
            }
            else
            {
                ShowTrayNotification(
                    $"{WsmConstants.AppDisplayName} - 托盘通知",
                    $"批量{actionText}完成：成功 {success}，跳过 {skipped}，失败 {failed}。",
                    ToolTipIcon.Warning);
            }

            await RefreshServiceListOnUiAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ShowTrayNotification(
                $"{WsmConstants.AppDisplayName} - 托盘通知",
                "托盘批量操作异常：" + ex.Message,
                ToolTipIcon.Error);
        }
        finally
        {
            _isBatchOperating = false;
            await RefreshBatchMenuStateAsync().ConfigureAwait(false);
        }
    }

    private async Task RefreshBatchMenuStateAsync()
    {
        if (_statusItem == null || _startAllItem == null || _stopAllItem == null)
        {
            return;
        }

        if (_isBatchOperating)
        {
            _statusItem.Text = "服务状态：统计中...";
            _startAllItem.Enabled = false;
            _stopAllItem.Enabled = false;
            return;
        }

        try
        {
            var services = await _serviceRepository.GetAllAsync().ConfigureAwait(false);
            if (services.Count == 0)
            {
                _statusItem.Text = "服务状态：0/0";
                _startAllItem.Enabled = false;
                _stopAllItem.Enabled = false;
                return;
            }

            var runtimeStatuses = await Task.WhenAll(services.Select(x => _winSwHostService.GetStatusAsync(x.Id))).ConfigureAwait(false);
            var runningCount = runtimeStatuses.Count(status =>
                status == ServiceRuntimeStatus.Running || status == ServiceRuntimeStatus.StartPending);
            var canStartAny = runtimeStatuses.Any(status =>
                status != ServiceRuntimeStatus.Running && status != ServiceRuntimeStatus.StartPending);
            var canStopAny = runtimeStatuses.Any(status =>
                status == ServiceRuntimeStatus.Running || status == ServiceRuntimeStatus.StartPending);
            _statusItem.Text = $"服务状态：{runningCount}/{services.Count}";
            _startAllItem.Enabled = canStartAny;
            _stopAllItem.Enabled = canStopAny;
        }
        catch
        {
            _statusItem.Text = "服务状态：获取失败";
            _startAllItem.Enabled = false;
            _stopAllItem.Enabled = false;
        }
    }

    private void ShowInfoOnUi(string message)
    {
        Application.Current.Dispatcher.Invoke(() => _snackbarService.ShowInfo(message));
    }

    private void ShowSuccessOnUi(string message)
    {
        Application.Current.Dispatcher.Invoke(() => _snackbarService.ShowSuccess(message));
    }

    private void ShowWarningOnUi(string message)
    {
        Application.Current.Dispatcher.Invoke(() => _snackbarService.ShowWarning(message));
    }

    private void ShowErrorOnUi(string message)
    {
        Application.Current.Dispatcher.Invoke(() => _snackbarService.ShowError(message));
    }

    private void ShowTrayNotification(string title, string message, ToolTipIcon icon)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_notifyIcon == null)
            {
                _snackbarService.ShowInfo(message);
                return;
            }

            try
            {
                _notifyIcon.Visible = true;
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.BalloonTipIcon = icon;
                _notifyIcon.ShowBalloonTip(3000);
            }
            catch
            {
                // 托盘气泡可能被系统策略拦截，兜底给应用内提示。
                if (icon == ToolTipIcon.Error)
                {
                    _snackbarService.ShowError(message);
                }
                else if (icon == ToolTipIcon.Warning)
                {
                    _snackbarService.ShowWarning(message);
                }
                else
                {
                    _snackbarService.ShowInfo(message);
                }
            }
        });
    }

    private Task RefreshServiceListOnUiAsync()
    {
        if (_mainWindow == null)
        {
            return Task.CompletedTask;
        }

        return _mainWindow.Dispatcher.InvokeAsync(() =>
        {
            if (_serviceListViewModel.RefreshCommand.CanExecute(null))
            {
                _serviceListViewModel.RefreshCommand.Execute(null);
            }
        }).Task;
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "wsm-logo-64.png");
            if (!File.Exists(iconPath))
            {
                return (Icon)SystemIcons.Application.Clone();
            }

            using var bitmap = new Bitmap(iconPath);
            var iconHandle = bitmap.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(iconHandle).Clone();
            }
            finally
            {
                DestroyIcon(iconHandle);
            }
        }
        catch
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
