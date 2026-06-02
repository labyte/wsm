using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSM.App.Shared.Models;
using WSM.App.Shared.Services;
using WSM.Core.Interfaces;
using WSM.Core.Models;
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
    }

    public ObservableCollection<ServiceConsoleOption> ServiceOptions { get; }
    public ObservableCollection<int> MaxLineOptions { get; } = new() { 200, 500, 1000, 2000, 3000, 5000 };

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _logSchemeText = "—";

    [ObservableProperty]
    private string _logPathText = string.Empty;

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
    private async Task OpenLogDirectoryAsync()
    {
        var directory = ResolveLogDirectory();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            _snackbarService.ShowWarning("日志目录不存在，请确认日志路径配置正确。");
            return;
        }

        Process.Start(new ProcessStartInfo(directory)
        {
            UseShellExecute = true
        });
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

            if (clearResult.ClearedCount > 0)
            {
                _snackbarService.ShowSuccess("综合操作日志已清空（operations.log）");
            }
            else
            {
                _snackbarService.ShowInfo("未清理到 operations.log，请确认文件路径与权限。");
            }

            if (clearResult.Failures.Count > 0)
            {
                var preview = string.Join(Environment.NewLine, clearResult.Failures
                    .Take(5)
                    .Select(x => $"{x.FilePath}（{x.Reason}）"));
                _snackbarService.ShowWarning("operations.log 清空失败：" + Environment.NewLine + preview);
            }

            await RefreshAsync().ConfigureAwait(true);
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

        if (wrapperClearResult.ClearedCount > 0)
        {
            _snackbarService.ShowSuccess($"{SelectedService.DisplayName} wrapper 日志已清空（{wrapperClearResult.ClearedCount} 个文件）");
        }
        else
        {
            _snackbarService.ShowInfo("未清理到可写 wrapper 日志文件，请确认文件占用与权限。");
        }

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

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        await _consoleLogHelper.CopyToClipboardAsync(DisplayText).ConfigureAwait(true);
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
        ApplyLogLines(logLines);
        LogSchemeText = "综合操作日志";
        LogPathText = string.IsNullOrWhiteSpace(operationLogPath) ? "—" : operationLogPath;
    }

    private async Task RefreshServiceWrapperAsync()
    {
        var serviceId = SelectedService?.ServiceId;
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            DisplayText = string.Empty;
            LogSchemeText = "—";
            LogPathText = "—";
            return;
        }

        var logLines = await Task.Run(() =>
            _logReader.ReadMergedWrapperLogs(new List<string> { serviceId! }, SelectedMaxLines)).ConfigureAwait(true);
        ApplyLogLines(logLines);

        var wrapperSource = _logReader.ResolveWrapperLogSource(serviceId!);
        LogSchemeText = $"{ServiceLogSourceStatusFormatter.FormatScheme(wrapperSource)} · wrapper";
        LogPathText = ServiceLogSourceStatusFormatter.FormatPath(wrapperSource);
    }

    private void ApplyLogLines(IReadOnlyList<ServiceLogLine> logLines)
    {
        var lines = logLines.Select(x => x.DisplayText).ToList();
        DisplayText = lines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine;
    }

    private string? ResolveLogDirectory()
    {
        if (IsCombinedView)
        {
            var operationLogPath = _paths.OperationLogPath;
            if (!string.IsNullOrWhiteSpace(operationLogPath))
            {
                var operationLogDirectory = Path.GetDirectoryName(operationLogPath);
                if (!string.IsNullOrWhiteSpace(operationLogDirectory) && Directory.Exists(operationLogDirectory))
                {
                    return operationLogDirectory;
                }
            }

            return Directory.Exists(_paths.AppLogsDirectory) ? _paths.AppLogsDirectory : null;
        }

        var serviceId = SelectedService?.ServiceId;
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return null;
        }

        var logsDirectory = _paths.GetServiceLogsDirectory(serviceId!);
        if (Directory.Exists(logsDirectory))
        {
            return logsDirectory;
        }

        var serviceDirectory = _paths.GetServiceDirectory(serviceId);
        return Directory.Exists(serviceDirectory) ? serviceDirectory : null;
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
