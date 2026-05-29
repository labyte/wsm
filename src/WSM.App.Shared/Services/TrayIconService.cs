using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using WSM.Core;
using Application = System.Windows.Application;

namespace WSM.App.Shared.Services;

/// <summary>
/// 基于 NotifyIcon 的托盘实现，兼容 Legacy（Win7）与 Modern。
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private readonly ISnackbarService _snackbarService;
    private NotifyIcon? _notifyIcon;
    private Window? _mainWindow;
    private bool _isExplicitExit;
    private bool _disposed;

    public TrayIconService(ISnackbarService snackbarService)
    {
        _snackbarService = snackbarService;
    }

    public bool MinimizeOnClose { get; set; } = true;

    public void Attach(Window mainWindow)
    {
        if (_notifyIcon != null)
        {
            throw new InvalidOperationException("托盘已初始化。");
        }

        _mainWindow = mainWindow;
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.StateChanged += OnMainWindowStateChanged;

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = WsmConstants.AppDisplayName,
            Visible = true
        };

        _notifyIcon.DoubleClick += OnTrayDoubleClick;
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
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
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("显示主窗口", null, (_, _) => ShowMainWindow());
        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());

        var startAllItem = new ToolStripMenuItem("全部启动服务")
        {
            Enabled = false
        };
        var stopAllItem = new ToolStripMenuItem("全部停止服务")
        {
            Enabled = false
        };
        menu.Items.Add(startAllItem);
        menu.Items.Add(stopAllItem);
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
}
