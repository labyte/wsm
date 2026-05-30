using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSM.App.Shared.Models;
using WSM.App.Shared.Services;
using WSM.Core.Interfaces;
using WSM.Infrastructure.Logging;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 服务控制台 ViewModel（托管服务 wrapper/out/err 日志）。
/// </summary>
public partial class ServiceConsoleViewModel : ObservableObject, INavigationAware
{
    private readonly IServiceRepository _serviceRepository;
    private readonly ServiceLogReader _logReader;
    private readonly ConsoleLogHelper _consoleLogHelper;
    private readonly DispatcherTimer _refreshTimer;
    private string? _pendingServiceId;

    public ServiceConsoleViewModel(
        IServiceRepository serviceRepository,
        ServiceLogReader logReader,
        ConsoleLogHelper consoleLogHelper)
    {
        _serviceRepository = serviceRepository;
        _logReader = logReader;
        _consoleLogHelper = consoleLogHelper;
        ServiceOptions = new ObservableCollection<ServiceConsoleOption> { ServiceConsoleOption.All };

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) =>
        {
            if (IsTracking)
            {
                _ = RefreshAsync();
            }
        };
    }

    public ObservableCollection<ServiceConsoleOption> ServiceOptions { get; }
    public ObservableCollection<int> MaxLineOptions { get; } = new() { 200, 500, 1000, 2000, 3000, 5000 };

    [ObservableProperty]
    private ServiceConsoleOption? _selectedService = ServiceConsoleOption.All;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "选择服务以查看日志";

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private bool _isTracking = true;

    [ObservableProperty]
    private bool _isWrapEnabled = true;

    [ObservableProperty]
    private int _selectedMaxLines = 1000;

    partial void OnSelectedServiceChanged(ServiceConsoleOption? value)
    {
        _ = RefreshAsync();
    }

    partial void OnIsTrackingChanged(bool value)
    {
        if (value)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnSelectedMaxLinesChanged(int value)
    {
        if (value <= 0)
        {
            SelectedMaxLines = 1000;
            return;
        }

        _ = RefreshAsync();
    }

    public void OnNavigatedTo()
    {
        _ = LoadServicesAsync();
        _ = RefreshAsync();
        _refreshTimer.Start();
    }

    /// <summary>
    /// 聚焦到指定服务日志；传 null 时回到全部服务视图。
    /// </summary>
    public void FocusService(string? serviceId)
    {
        _pendingServiceId = string.IsNullOrWhiteSpace(serviceId) ? null : serviceId;
        TryApplyPendingServiceSelection();
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var serviceIds = ResolveTargetServiceIds();
            if (serviceIds.Count == 0)
            {
                DisplayText = string.Empty;
                StatusText = "暂无已安装服务";
                return;
            }

            var logLines = _logReader.ReadMergedLogs(serviceIds, SelectedMaxLines);
            DisplayText = string.Join(Environment.NewLine, logLines.Select(x => x.DisplayText));

            var scopeText = SelectedService?.ServiceId == null
                ? "全部服务"
                : SelectedService.DisplayName;
            var trackText = IsTracking ? "实时跟踪" : "暂停跟踪";
            StatusText = $"{scopeText} · {logLines.Count}/{SelectedMaxLines} 行 · {trackText}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CopyAll()
    {
        _consoleLogHelper.CopyToClipboard(DisplayText);
    }

    [RelayCommand]
    private void Export()
    {
        var serviceName = SelectedService?.ServiceId ?? "all-services";
        _consoleLogHelper.ExportToFile(DisplayText, $"service-{serviceName}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        var serviceIds = ResolveTargetServiceIds();
        if (serviceIds.Count > 0)
        {
            await Task.Run(() => _logReader.ClearLogs(serviceIds)).ConfigureAwait(true);
        }

        DisplayText = string.Empty;
        StatusText = "日志已清空（文件内容已删除）";
    }

    private async Task LoadServicesAsync()
    {
        var services = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
        var discoveredIds = _logReader.DiscoverServiceIdsWithLogs();
        var currentId = _pendingServiceId ?? SelectedService?.ServiceId;

        ServiceOptions.Clear();
        ServiceOptions.Add(ServiceConsoleOption.All);

        foreach (var service in services.OrderBy(x => x.DisplayName))
        {
            ServiceOptions.Add(new ServiceConsoleOption(service.Id, service.DisplayName));
        }

        foreach (var serviceId in discoveredIds)
        {
            if (ServiceOptions.Any(x => string.Equals(x.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ServiceOptions.Add(new ServiceConsoleOption(serviceId, serviceId));
        }

        SelectedService = ServiceOptions.FirstOrDefault(x => x.ServiceId == currentId)
            ?? ServiceConsoleOption.All;

        _pendingServiceId = null;
        await RefreshAsync().ConfigureAwait(true);
    }

    private void TryApplyPendingServiceSelection()
    {
        if (_pendingServiceId == null)
        {
            SelectedService = ServiceConsoleOption.All;
            return;
        }

        var matched = ServiceOptions.FirstOrDefault(x => string.Equals(x.ServiceId, _pendingServiceId, StringComparison.OrdinalIgnoreCase));
        if (matched != null)
        {
            SelectedService = matched;
        }
    }

    private System.Collections.Generic.List<string> ResolveTargetServiceIds()
    {
        if (SelectedService?.ServiceId != null)
        {
            return new System.Collections.Generic.List<string> { SelectedService.ServiceId };
        }

        var configuredServiceIds = ServiceOptions
            .Where(x => x.ServiceId != null)
            .Select(x => x.ServiceId!)
            .Distinct()
            .ToList();

        if (configuredServiceIds.Count > 0)
        {
            return configuredServiceIds;
        }

        return _logReader.DiscoverServiceIdsWithLogs().ToList();
    }
}
