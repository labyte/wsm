using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSM.App.Shared.Models;
using WSM.App.Shared.Services;
using WSM.Core.Interfaces;
using WSM.Infrastructure.Logging;
using WSM.Infrastructure.Paths;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// WSM 操作日志 ViewModel（综合视图读 operations.log，单服务读 wrapper.log）。
/// </summary>
public partial class LogViewerViewModel : ObservableObject, INavigationAware
{
    private readonly IServiceRepository _serviceRepository;
    private readonly ServiceLogReader _logReader;
    private readonly ConsoleLogHelper _consoleLogHelper;
    private readonly ISnackbarService _snackbarService;
    private readonly WsmPaths _paths;
    private readonly DispatcherTimer _refreshTimer;

    public LogViewerViewModel(
        IServiceRepository serviceRepository,
        ServiceLogReader logReader,
        ConsoleLogHelper consoleLogHelper,
        ISnackbarService snackbarService,
        WsmPaths paths)
    {
        _serviceRepository = serviceRepository;
        _logReader = logReader;
        _consoleLogHelper = consoleLogHelper;
        _snackbarService = snackbarService;
        _paths = paths;
        ServiceOptions = new ObservableCollection<ServiceConsoleOption> { ServiceConsoleOption.Combined };
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
    private ServiceConsoleOption? _selectedService = ServiceConsoleOption.Combined;

    [ObservableProperty]
    private bool _isTracking = true;

    [ObservableProperty]
    private bool _isWrapEnabled = true;

    [ObservableProperty]
    private int _selectedMaxLines = 500;

    [ObservableProperty]
    private bool _isLoading;

    private bool IsCombinedView => SelectedService?.ServiceId == null;

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
        if (IsCombinedView)
        {
            var operationLogPath = _paths.OperationLogPath;
            var clearResult = await Task.Run(() =>
                _logReader.ClearLogFilesDetailed(new[] { operationLogPath })).ConfigureAwait(true);
            DisplayText = string.Empty;
            StatusText = clearResult.ClearedCount > 0
                ? "综合操作日志已清空（operations.log）"
                : "未清理到 operations.log，请确认文件路径与权限。";

            if (clearResult.Failures.Count > 0)
            {
                var preview = string.Join(Environment.NewLine, clearResult.Failures
                    .Take(5)
                    .Select(x => $"{x.FilePath}（{x.Reason}）"));
                _snackbarService.ShowWarning("operations.log 清空失败：" + Environment.NewLine + preview);
            }

            return;
        }

        if (SelectedService?.ServiceId == null)
        {
            return;
        }

        var serviceIds = new List<string> { SelectedService.ServiceId };
        var wrapperClearResult = await Task.Run(() =>
            _logReader.ClearWrapperLogsDetailed(serviceIds)).ConfigureAwait(true);
        DisplayText = string.Empty;
        StatusText = wrapperClearResult.ClearedCount > 0
            ? $"{SelectedService.DisplayName} wrapper 日志已清空（{wrapperClearResult.ClearedCount} 个文件）"
            : "未清理到可写 wrapper 日志文件，请确认文件占用与权限。";

        if (wrapperClearResult.Failures.Count > 0)
        {
            var preview = string.Join(Environment.NewLine, wrapperClearResult.Failures
                .Take(5)
                .Select(x => $"{x.FilePath}（{x.Reason}）"));
            var moreHint = wrapperClearResult.Failures.Count > 5
                ? $"{Environment.NewLine}... 其余 {wrapperClearResult.Failures.Count - 5} 个文件未展示"
                : string.Empty;
            _snackbarService.ShowWarning("以下 wrapper 日志文件清空失败：" + Environment.NewLine + preview + moreHint);
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
        var suffix = IsCombinedView
            ? "combined-operations"
            : $"wrapper-{SelectedService?.ServiceId ?? "service"}";
        _consoleLogHelper.ExportToFile(DisplayText, $"wsm-{suffix}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
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
            if (IsCombinedView)
            {
                await RefreshCombinedAsync().ConfigureAwait(true);
                return;
            }

            await RefreshServiceWrapperAsync().ConfigureAwait(true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshCombinedAsync()
    {
        var operationLogPath = _paths.OperationLogPath;
        var logLines = await Task.Run(() =>
            _logReader.ReadOperationLog(operationLogPath, SelectedMaxLines)).ConfigureAwait(true);
        ApplyDisplay(logLines, "综合(operations.log)", operationLogPath);
    }

    private async Task RefreshServiceWrapperAsync()
    {
        var serviceId = SelectedService?.ServiceId;
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            DisplayText = string.Empty;
            StatusText = "请选择服务";
            return;
        }

        var logLines = await Task.Run(() =>
            _logReader.ReadMergedWrapperLogs(new List<string> { serviceId! }, SelectedMaxLines)).ConfigureAwait(true);
        var scopeName = SelectedService?.DisplayName ?? serviceId;
        ApplyDisplay(logLines, $"{scopeName}(wrapper)", null);
    }

    private void ApplyDisplay(
        IReadOnlyList<ServiceLogLine> logLines,
        string scopeText,
        string? sourceFilePath)
    {
        var lines = logLines.Select(x => x.DisplayText).ToList();
        DisplayText = lines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine;

        var trackText = IsTracking ? "实时跟踪" : "暂停跟踪";
        var fileHint = string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath)
            ? string.Empty
            : $" · {sourceFilePath}";
        StatusText = $"{scopeText} · {lines.Count}/{SelectedMaxLines} 行 · {trackText}{fileHint}";
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
        _ = RefreshAsync();
    }

    private async Task LoadServicesAsync()
    {
        var services = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
        var currentId = SelectedService?.ServiceId;

        ServiceOptions.Clear();
        ServiceOptions.Add(ServiceConsoleOption.Combined);

        foreach (var service in services.OrderBy(x => x.DisplayName))
        {
            ServiceOptions.Add(new ServiceConsoleOption(service.Id, service.DisplayName));
        }

        SelectedService = ServiceOptions.FirstOrDefault(x => x.ServiceId == currentId)
            ?? ServiceConsoleOption.Combined;
    }
}
