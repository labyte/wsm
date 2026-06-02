using System;
using System.Threading.Tasks;
using System.Windows;

namespace WSM.App.Shared.Services;

/// <summary>
/// 系统托盘服务。
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// 关闭主窗口时是否最小化到托盘（而非退出）。
    /// </summary>
    bool MinimizeOnClose { get; set; }

    /// <summary>
    /// 绑定主窗口并显示托盘图标。
    /// </summary>
    void Attach(Window mainWindow);

    /// <summary>
    /// 显示并激活主窗口。
    /// </summary>
    void ShowMainWindow();

    /// <summary>
    /// 隐藏主窗口到托盘。
    /// </summary>
    void HideToTray(bool showBalloon = true);

    /// <summary>
    /// 退出应用程序。
    /// </summary>
    void RequestExit();

    /// <summary>
    /// 刷新托盘悬停提示与右键菜单中的服务运行统计。
    /// </summary>
    Task RefreshMonitoringStateAsync();
}
