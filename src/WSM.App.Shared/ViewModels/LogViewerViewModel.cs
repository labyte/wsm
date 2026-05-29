using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSM.App.Shared.Services;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// WSM 操作日志 ViewModel。
/// </summary>
public partial class LogViewerViewModel : ObservableObject, INavigationAware
{
    private readonly AppOperationLogService _logService;
    private readonly ConsoleLogHelper _consoleLogHelper;

    public LogViewerViewModel(AppOperationLogService logService, ConsoleLogHelper consoleLogHelper)
    {
        _logService = logService;
        _consoleLogHelper = consoleLogHelper;
        _logService.EntriesChanged += (_, _) => RebuildDisplayText();
        RebuildDisplayText();
    }

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public void OnNavigatedTo()
    {
        RebuildDisplayText();
    }

    [RelayCommand]
    private void Clear()
    {
        _logService.Clear();
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

    private void RebuildDisplayText()
    {
        var lines = _logService.Entries
            .OrderBy(x => x.TimestampLocal)
            .Select(x => x.DisplayText)
            .ToList();

        DisplayText = string.Join(Environment.NewLine, lines);
        StatusText = $"{lines.Count} 条记录";
    }
}
