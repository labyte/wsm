using System;
using System.Collections.Generic;
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
/// WSM 操作日志 ViewModel。
/// </summary>
public partial class LogViewerViewModel : ObservableObject, INavigationAware
{
    private readonly IServiceRepository _serviceRepository;
    private readonly ServiceLogReader _logReader;
    private readonly ConsoleLogHelper _consoleLogHelper;
    private readonly DispatcherTimer _refreshTimer;

    public LogViewerViewModel(
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
        StatusText = "正在加载历史日志…";
    }

    public ObservableCollection<ServiceConsoleOption> ServiceOptions { get; }
    public ObservableCollection<int> MaxLineOptions { get; } = new() { 200, 500, 1000, 2000, 3000, 5000 };

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private ServiceConsoleOption? _selectedService = ServiceConsoleOption.All;

    [ObservableProperty]
    private bool _isTracking = true;

    [ObservableProperty]
    private bool _isWrapEnabled = true;

    [ObservableProperty]
    private int _selectedMaxLines = 500;

    [ObservableProperty]
    private bool _isLoading;

    partial void OnSelectedServiceChanged(ServiceConsoleOption? value)
    {
        _ = RefreshAsync();
    }

    public void OnNavigatedTo()
    {
        _ = LoadServicesAsync();
        _ = RefreshAsync();
        _refreshTimer.Start();
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        var serviceIds = ResolveTargetServiceIds();
        if (serviceIds.Count > 0)
        {
            var clearedCount = await Task.Run(() => _logReader.ClearWrapperLogs(serviceIds)).ConfigureAwait(true);
            DisplayText = string.Empty;
            StatusText = $"WSM 操作日志已清空（已处理 {clearedCount} 个 wrapper 文件）";
            return;
        }

        DisplayText = string.Empty;
        StatusText = "暂无可清空的 WSM 操作日志";
    }

    [RelayCommand]
    private void CopyAll()
    {
        _consoleLogHelper.CopyToClipboard(DisplayText);
    }

    [RelayCommand]
    private void Export()
    {
        _consoleLogHelper.ExportToFile(DisplayText, $"wsm-operations-{DateTime.Now:yyyyMMdd-HHmmss}.log");
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
            var serviceIds = await Task.Run(ResolveTargetServiceIds).ConfigureAwait(true);
            if (serviceIds.Count == 0)
            {
                DisplayText = string.Empty;
                StatusText = "暂无 WSM 操作日志";
                return;
            }

            var logLines = await Task.Run(() => _logReader.ReadMergedWrapperLogs(serviceIds, SelectedMaxLines)).ConfigureAwait(true);
            var lines = logLines.Select(x => x.DisplayText).ToList();

            DisplayText = lines.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine;
            var trackText = IsTracking ? "实时跟踪" : "暂停跟踪";
            var scopeText = SelectedService?.ServiceId == null
                ? "全部服务(wrapper)"
                : $"{SelectedService.DisplayName}(wrapper)";
            StatusText = $"{scopeText} · {lines.Count}/{SelectedMaxLines} 行 · {trackText}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedMaxLinesChanged(int value)
    {
        if (value <= 0)
        {
            SelectedMaxLines = 500;
            return;
        }

        _ = RefreshAsync();
    }

    partial void OnIsTrackingChanged(bool value)
    {
        if (value)
        {
            _ = RefreshAsync();
        }
    }

    private async Task LoadServicesAsync()
    {
        var services = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
        var discoveredIds = _logReader.DiscoverServiceIdsWithWrapperLogs();
        var currentId = SelectedService?.ServiceId;

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
    }

    private List<string> ResolveTargetServiceIds()
    {
        if (SelectedService?.ServiceId != null)
        {
            return new List<string> { SelectedService.ServiceId };
        }

        var serviceIds = ServiceOptions
            .Where(x => x.ServiceId != null)
            .Select(x => x.ServiceId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (serviceIds.Count > 0)
        {
            return serviceIds;
        }

        return _logReader.DiscoverServiceIdsWithWrapperLogs().ToList();
    }
}
