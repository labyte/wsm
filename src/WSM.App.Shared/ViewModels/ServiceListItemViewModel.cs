using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using WSM.Core.Models;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 服务列表行 ViewModel。
/// </summary>
public partial class ServiceListItemViewModel : ObservableObject
{
    public ServiceListItemViewModel(ManagedService service)
    {
        ServiceId = service.Id;
        DisplayName = service.DisplayName;
        ExecutablePath = service.ExecutablePath;
        ProgramDirectory = !string.IsNullOrWhiteSpace(service.WorkingDirectory)
            ? service.WorkingDirectory
            : Path.GetDirectoryName(service.ExecutablePath) ?? string.Empty;
        ConfigFilePath = string.Empty;
    }

    public string ServiceId { get; }

    public string DisplayName { get; }

    public string ExecutablePath { get; }

    /// <summary>
    /// 程序目录（工作目录）。
    /// </summary>
    public string ProgramDirectory { get; }

    public string ConfigFilePath { get; private set; }

    [ObservableProperty]
    private ServiceRuntimeStatus _status = ServiceRuntimeStatus.NotInstalled;

    [ObservableProperty]
    private string _statusText = "未知";

    [ObservableProperty]
    private string _startedAtText = "-";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isSelected;

    public void SetConfigFilePath(string path)
    {
        ConfigFilePath = path;
    }

    public void UpdateRuntimeInfo(ServiceRuntimeInfo info)
    {
        Status = info.Status;
        StatusText = MapStatusText(info.Status);
        StartedAtText = info.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        IsRunning = info.Status == ServiceRuntimeStatus.Running || info.Status == ServiceRuntimeStatus.StartPending;
    }

    private static string MapStatusText(ServiceRuntimeStatus status)
    {
        switch (status)
        {
            case ServiceRuntimeStatus.Running:
                return "运行中";
            case ServiceRuntimeStatus.Stopped:
                return "已停止";
            case ServiceRuntimeStatus.StartPending:
                return "启动中";
            case ServiceRuntimeStatus.StopPending:
                return "停止中";
            case ServiceRuntimeStatus.NotInstalled:
                return "未安装";
            default:
                return "异常";
        }
    }
}
