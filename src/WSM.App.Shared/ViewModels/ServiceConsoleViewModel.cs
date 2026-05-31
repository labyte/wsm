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
    private readonly ISnackbarService _snackbarService;
    private readonly DispatcherTimer _refreshTimer;
    private string? _pendingServiceId;

    public ServiceConsoleViewModel(
        IServiceRepository serviceRepository,
        ServiceLogReader logReader,
        ConsoleLogHelper consoleLogHelper,
        ISnackbarService snackbarService)
    {
        _serviceRepository = serviceRepository;
        _logReader = logReader;
        _consoleLogHelper = consoleLogHelper;
        _snackbarService = snackbarService;
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
        // 勾选状态变化后立即刷新状态栏文案，确保“实时跟踪/暂停跟踪”同步显示。
        _ = RefreshAsync();
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

            var serviceConfigs = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
            var serviceConfigMap = serviceConfigs.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var externalLogFiles = BuildExternalLogFileMap(serviceIds, serviceConfigMap);
            var effectiveMaxLines = SelectedMaxLines;

            foreach (var serviceId in serviceIds)
            {
                if (!serviceConfigMap.TryGetValue(serviceId, out var config))
                {
                    continue;
                }

                if (config.LogSourceMode != WSM.Core.Models.ServiceLogSourceMode.ExternalFile)
                {
                    continue;
                }

                if (serviceIds.Count == 1 && config.ExternalLogTailLines > 0)
                {
                    effectiveMaxLines = config.ExternalLogTailLines;
                    if (IsTracking != config.ExternalLogRealtimeTracking)
                    {
                        IsTracking = config.ExternalLogRealtimeTracking;
                    }
                }
            }

            var logLines = _logReader.ReadMergedLogs(serviceIds, externalLogFiles, effectiveMaxLines);
            var effectiveLines = logLines
                .SelectMany(x => SplitToNonEmptyLines(x.DisplayText))
                .ToList();
            var joinedText = string.Join(Environment.NewLine, effectiveLines);
            DisplayText = effectiveLines.Count == 0
                ? string.Empty
                : joinedText + Environment.NewLine + Environment.NewLine;

            var scopeText = SelectedService?.ServiceId == null
                ? "全部服务"
                : SelectedService.DisplayName;
            var trackText = IsTracking ? "实时跟踪" : "暂停跟踪";
            StatusText = $"{scopeText} · {effectiveLines.Count}/{effectiveMaxLines} 行 · {trackText}";
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
        var managedClearResult = new LogClearResult();
        var externalClearResult = new LogClearResult();
        if (serviceIds.Count > 0)
        {
            var serviceConfigs = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
            var serviceConfigMap = serviceConfigs.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var externalLogFiles = BuildExternalLogFileMap(serviceIds, serviceConfigMap);

            managedClearResult = await Task.Run(() => _logReader.ClearLogsDetailed(serviceIds)).ConfigureAwait(true);
            externalClearResult = await Task.Run(() => _logReader.ClearLogFilesDetailed(externalLogFiles.Values)).ConfigureAwait(true);
        }

        var clearedCount = managedClearResult.ClearedCount + externalClearResult.ClearedCount;
        var failures = managedClearResult.Failures
            .Concat(externalClearResult.Failures)
            .GroupBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        DisplayText = string.Empty;
        StatusText = clearedCount > 0
            ? $"日志已清空（共清理 {clearedCount} 个文件）"
            : "未清理到可写日志文件，请确认服务日志目录与权限。";

        if (failures.Count > 0)
        {
            var preview = string.Join(Environment.NewLine, failures
                .Take(5)
                .Select(x => $"{x.FilePath}（{x.Reason}）"));
            var moreHint = failures.Count > 5 ? $"{Environment.NewLine}... 其余 {failures.Count - 5} 个文件未展示" : string.Empty;
            _snackbarService.ShowWarning("以下日志文件清空失败：" + Environment.NewLine + preview + moreHint);
        }
    }

    private async Task LoadServicesAsync()
    {
        var services = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
        var currentId = _pendingServiceId ?? SelectedService?.ServiceId;

        ServiceOptions.Clear();
        ServiceOptions.Add(ServiceConsoleOption.All);

        foreach (var service in services.OrderBy(x => x.DisplayName))
        {
            ServiceOptions.Add(new ServiceConsoleOption(service.Id, service.DisplayName));
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

        return new System.Collections.Generic.List<string>();
    }

    private System.Collections.Generic.Dictionary<string, string> BuildExternalLogFileMap(
        System.Collections.Generic.IReadOnlyList<string> serviceIds,
        System.Collections.Generic.IReadOnlyDictionary<string, Core.Models.ManagedService> serviceConfigMap)
    {
        var externalLogFiles = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var serviceId in serviceIds)
        {
            if (!serviceConfigMap.TryGetValue(serviceId, out var config))
            {
                continue;
            }

            if (config.LogSourceMode != Core.Models.ServiceLogSourceMode.ExternalFile)
            {
                continue;
            }

            var externalLogPath = _logReader.ResolveLatestExternalLogFile(
                config.ExternalLogDirectoryPath,
                config.ExternalLogFileExtensions);
            if (string.IsNullOrWhiteSpace(externalLogPath)
                && !string.IsNullOrWhiteSpace(config.ExternalLogFilePath))
            {
                // 兼容旧配置：若未配置目录规则，则回退到历史的单文件路径。
                externalLogPath = config.ExternalLogFilePath;
            }

            if (!string.IsNullOrWhiteSpace(externalLogPath))
            {
                externalLogFiles[serviceId] = externalLogPath ?? string.Empty;
            }
        }

        return externalLogFiles;
    }

    private static System.Collections.Generic.IEnumerable<string> SplitToNonEmptyLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }
}
