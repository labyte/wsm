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
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();
    }

    public ObservableCollection<ServiceConsoleOption> ServiceOptions { get; }

    [ObservableProperty]
    private ServiceConsoleOption? _selectedService = ServiceConsoleOption.All;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "选择服务以查看日志";

    [ObservableProperty]
    private string _displayText = string.Empty;

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

            var logLines = _logReader.ReadMergedLogs(serviceIds);
            DisplayText = string.Join(Environment.NewLine, logLines.Select(x => x.DisplayText));

            StatusText = SelectedService?.ServiceId == null
                ? $"全部服务 · {logLines.Count} 行"
                : $"{SelectedService.DisplayName} · {logLines.Count} 行";
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

    private async Task LoadServicesAsync()
    {
        var services = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
        var currentId = SelectedService?.ServiceId;

        ServiceOptions.Clear();
        ServiceOptions.Add(ServiceConsoleOption.All);

        foreach (var service in services.OrderBy(x => x.DisplayName))
        {
            ServiceOptions.Add(new ServiceConsoleOption(service.Id, service.DisplayName));
        }

        SelectedService = ServiceOptions.FirstOrDefault(x => x.ServiceId == currentId)
            ?? ServiceConsoleOption.All;
    }

    private System.Collections.Generic.List<string> ResolveTargetServiceIds()
    {
        if (SelectedService?.ServiceId != null)
        {
            return new System.Collections.Generic.List<string> { SelectedService.ServiceId };
        }

        return ServiceOptions
            .Where(x => x.ServiceId != null)
            .Select(x => x.ServiceId!)
            .Distinct()
            .ToList();
    }
}
