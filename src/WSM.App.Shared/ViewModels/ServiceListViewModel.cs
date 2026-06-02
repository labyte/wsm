using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using WSM.App.Shared.Navigation;
using WSM.App.Shared.Services;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Infrastructure.Paths;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 服务总览页 ViewModel。
/// </summary>
public partial class ServiceListViewModel : ObservableObject, INavigationAware
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IWinSwHostService _winSwHostService;
    private readonly ISnackbarService _snackbarService;
    private readonly ITrayIconService _trayIconService;
    private readonly WsmPaths _paths;
    private readonly INavigationService _navigationService;
    private readonly ServiceConsoleViewModel _serviceConsoleViewModel;
    private bool _isUpdatingSelection;

    public ServiceListViewModel(
        IServiceRepository serviceRepository,
        IWinSwHostService winSwHostService,
        ISnackbarService snackbarService,
        ITrayIconService trayIconService,
        WsmPaths paths,
        INavigationService navigationService,
        ServiceConsoleViewModel serviceConsoleViewModel)
    {
        _serviceRepository = serviceRepository;
        _winSwHostService = winSwHostService;
        _snackbarService = snackbarService;
        _trayIconService = trayIconService;
        _paths = paths;
        _navigationService = navigationService;
        _serviceConsoleViewModel = serviceConsoleViewModel;
        Services = new ObservableCollection<ServiceListItemViewModel>();
    }

    public ObservableCollection<ServiceListItemViewModel> Services { get; }
    public ObservableCollection<ServiceListItemViewModel> SelectedServices { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _isAllSelected;

    [ObservableProperty]
    private bool _isConfirmingBatchUninstall;

    public void OnNavigatedTo()
    {
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
            var services = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
            foreach (var service in Services)
            {
                service.PropertyChanged -= ServiceItemOnPropertyChanged;
            }

            Services.Clear();
            SelectedServices.Clear();

            foreach (var service in services.OrderBy(x => x.DisplayName))
            {
                var item = new ServiceListItemViewModel(service);
                item.SetConfigFilePath(_paths.GetServiceConfigPath(service.Id));
                item.PropertyChanged += ServiceItemOnPropertyChanged;
                Services.Add(item);

                var runtime = await _winSwHostService.GetRuntimeInfoAsync(service.Id).ConfigureAwait(true);
                item.UpdateRuntimeInfo(runtime);
            }

            UpdateSelectAllState();
            await RefreshTrayMonitoringStateAsync().ConfigureAwait(true);
        }
        finally
        {
            IsLoading = false;
            UpdateEmptyState();
        }
    }

    private void UpdateEmptyState()
    {
        IsEmpty = !IsLoading && Services.Count == 0;
    }

    [RelayCommand]
    private async Task StartAsync(ServiceListItemViewModel? item)
    {
        await ExecuteServiceActionAsync(item, id => _winSwHostService.StartAsync(id)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StopAsync(ServiceListItemViewModel? item)
    {
        await ExecuteServiceActionAsync(item, id => _winSwHostService.StopAsync(id)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartSelectedAsync()
    {
        await ExecuteBatchServiceActionAsync(
            SelectedServices.Where(x => !x.IsRunning).ToList(),
            id => _winSwHostService.StartAsync(id),
            "一键启动完成。").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StopSelectedAsync()
    {
        await ExecuteBatchServiceActionAsync(
            SelectedServices.Where(x => x.IsRunning).ToList(),
            id => _winSwHostService.StopAsync(id),
            "一键停止完成。").ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenBatchUninstallConfirm()
    {
        if (SelectedServices.Count == 0)
        {
            _snackbarService.ShowInfo("请先选择至少一个服务。");
            IsConfirmingBatchUninstall = false;
            return;
        }

        foreach (var service in Services)
        {
            service.IsConfirmingUninstall = false;
        }

        IsConfirmingBatchUninstall = true;
    }

    [RelayCommand]
    private void CancelBatchUninstallConfirm()
    {
        IsConfirmingBatchUninstall = false;
    }

    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        if (SelectedServices.Count == 0)
        {
            _snackbarService.ShowInfo("请先选择至少一个服务。");
            IsConfirmingBatchUninstall = false;
            return;
        }

        IsConfirmingBatchUninstall = false;
        var targets = SelectedServices.ToList();
        var failed = 0;
        var removed = 0;

        foreach (var item in targets)
        {
            item.IsBusy = true;
            try
            {
                var result = await _winSwHostService.UninstallAsync(item.ServiceId).ConfigureAwait(true);
                if (!result.Success)
                {
                    failed++;
                    _snackbarService.ShowError(result.Message);
                    continue;
                }

                item.PropertyChanged -= ServiceItemOnPropertyChanged;
                Services.Remove(item);
                SelectedServices.Remove(item);
                removed++;
            }
            finally
            {
                item.IsBusy = false;
                item.IsConfirmingUninstall = false;
            }
        }

        UpdateSelectAllState();
        UpdateEmptyState();

        if (failed == 0)
        {
            _snackbarService.ShowSuccess($"一键卸载完成，共卸载 {removed} 项。");
        }
        else
        {
            _snackbarService.ShowWarning($"一键卸载完成，成功 {removed} 项，失败 {failed} 项。");
        }

        await RefreshTrayMonitoringStateAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RestartAsync(ServiceListItemViewModel? item)
    {
        await ExecuteServiceActionAsync(item, id => _winSwHostService.RestartAsync(id)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleServiceStateAsync(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        if (item.IsRunning)
        {
            await StopAsync(item).ConfigureAwait(true);
            return;
        }

        await StartAsync(item).ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenConsoleLogs(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        _serviceConsoleViewModel.FocusService(item.ServiceId);
        _navigationService.NavigateTo(AppPage.ServiceConsole);
    }

    [RelayCommand]
    private async Task OpenConfigAsync(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        var service = await _serviceRepository.GetByIdAsync(item.ServiceId).ConfigureAwait(true);
        if (service == null)
        {
            _snackbarService.ShowWarning("未找到服务配置记录。");
            return;
        }

        var dialogState = BuildConfigDialogState(service);
        var dialogResult = await ShowConfigDialogAsync(dialogState).ConfigureAwait(true);

        if (dialogResult == ConfigDialogAction.Uninstall)
        {
            await UninstallAsync(item).ConfigureAwait(true);
            return;
        }

        if (dialogResult != ConfigDialogAction.Save)
        {
            return;
        }

        if (!TryApplyConfigEdits(service, dialogState, out var validationError))
        {
            _snackbarService.ShowWarning(validationError);
            return;
        }

        item.IsBusy = true;
        try
        {
            var refreshResult = await _winSwHostService.RefreshAsync(service).ConfigureAwait(true);
            if (!refreshResult.Success)
            {
                _snackbarService.ShowError(refreshResult.Message);
                return;
            }

            _snackbarService.ShowSuccess("配置已保存并刷新。通常无需重装服务；部分项可能需要重启服务后生效。");
            await RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenProgramDirectory(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        var directory = item.ProgramDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            _snackbarService.ShowWarning("程序目录不存在。");
            return;
        }

        OpenPath(directory);
    }

    [RelayCommand]
    private void OpenServiceDirectory(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        var directory = _paths.GetServiceDirectory(item.ServiceId);
        if (!Directory.Exists(directory))
        {
            _snackbarService.ShowWarning("服务部署目录不存在。");
            return;
        }

        OpenPath(directory);
    }

    [RelayCommand]
    private async Task UninstallAsync(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        item.IsConfirmingUninstall = false;

        item.IsBusy = true;
        try
        {
            var result = await _winSwHostService.UninstallAsync(item.ServiceId).ConfigureAwait(true);
            if (result.Success)
            {
                _snackbarService.ShowSuccess(result.Message);
                item.PropertyChanged -= ServiceItemOnPropertyChanged;
                Services.Remove(item);
                SelectedServices.Remove(item);
                UpdateSelectAllState();
                UpdateEmptyState();
            }
            else
            {
                _snackbarService.ShowError(result.Message);
            }
        }
        finally
        {
            item.IsBusy = false;
            await RefreshTrayMonitoringStateAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void OpenUninstallConfirm(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        IsConfirmingBatchUninstall = false;

        // 先关闭其他项的卸载确认状态，确保一次只有一个项处于确认状态。
        foreach (var service in Services)
        {
            if (ReferenceEquals(service, item))
            {
                continue;
            }

            service.IsConfirmingUninstall = false;
        }

        if (item.IsConfirmingUninstall)
            item.IsConfirmingUninstall = false;

        item.IsConfirmingUninstall = true;
    }

    [RelayCommand]
    private void CancelUninstallConfirm(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        item.IsConfirmingUninstall = false;
    }

    private static ServiceConfigDraft BuildConfigDialogState(ManagedService service)
    {
        return ServiceConfigDraft.FromManagedService(service);
    }

    private static bool TryApplyConfigEdits(
        ManagedService service,
        ServiceConfigDraft dialogState,
        out string validationError)
    {
        if (!dialogState.TryValidate(out validationError))
        {
            return false;
        }

        dialogState.ApplyTo(service);
        return true;
    }

    private static async Task<ConfigDialogAction> ShowConfigDialogAsync(ServiceConfigDraft state)
    {
        var container = new StackPanel
        {
            Margin = new Thickness(24),
            MinWidth = 720
        };

        container.Children.Add(new TextBlock
        {
            Text = $"服务配置 - 服务ID：{state.Id}",
            FontSize = 18,
            FontWeight = FontWeights.Medium
        });

        container.Children.Add(new TextBlock
        {
            Text = "保存后会执行配置刷新（refresh），通常无需重新安装；部分参数可能需要重启服务后生效。",
            Margin = new Thickness(0, 8, 0, 12),
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap
        });
        var tabs = new TabControl();

        var basicPanel = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
        basicPanel.Children.Add(CreateLabeledTextBox("显示名称", state.DisplayName, value => state.DisplayName = value));
        basicPanel.Children.Add(CreateLabeledTextBox("描述", state.Description, value => state.Description = value));
        basicPanel.Children.Add(CreateLabeledTextBox("可执行路径", state.ExecutablePath, value => state.ExecutablePath = value));
        basicPanel.Children.Add(CreateLabeledTextBox("工作目录", state.WorkingDirectory, value => state.WorkingDirectory = value));
        basicPanel.Children.Add(CreateLabeledTextBox("启动参数", state.Arguments, value => state.Arguments = value));
        basicPanel.Children.Add(CreateLabeledOptionComboBox(
            "启动类型",
            state.StartMode,
            ServiceConfigUiOptions.StartModeOptions,
            value => state.StartMode = value));
        basicPanel.Children.Add(CreateLabeledCheckBox("启用延迟自动启动", state.DelayedAutoStart, value => state.DelayedAutoStart = value));
        basicPanel.Children.Add(CreateLabeledCheckBox("启用自动刷新配置", state.AutoRefresh, value => state.AutoRefresh = value));
        basicPanel.Children.Add(CreateLabeledCheckBox("隐藏程序窗口", state.HideWindow, value => state.HideWindow = value));
        basicPanel.Children.Add(CreateLabeledIntTextBox("停止超时（秒）", state.StopTimeoutSeconds, value => state.StopTimeoutSeconds = value));
        basicPanel.Children.Add(CreateLabeledCheckBox("安装后自动启动", state.StartAfterInstall, value => state.StartAfterInstall = value));
        tabs.Items.Add(new TabItem { Header = "基本", Content = CreateTabScrollContent(basicPanel) });

        var logPanel = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
        logPanel.Children.Add(CreateLabeledOptionComboBox(
            "日志方案",
            state.LogSourceMode,
            ServiceConfigUiOptions.LogSourceOptions,
            value => state.LogSourceMode = value));
        var logModeEditor = CreateLabeledOptionComboBox(
            "WinSW 模式",
            state.LogMode,
            ServiceConfigUiOptions.WinSwLogModeOptions,
            value => state.LogMode = value);
        var logSizeEditor = CreateLabeledIntTextBox("大小阈值（KB）", state.LogSizeThresholdKb, value => state.LogSizeThresholdKb = value);
        var logKeepFilesEditor = CreateLabeledIntTextBox("保留文件数", state.LogKeepFiles, value => state.LogKeepFiles = value);
        var externalLogDirectoryEditor = CreateLabeledTextBox("日志目录", state.ExternalLogDirectoryPath, value => state.ExternalLogDirectoryPath = value);
        var externalLogExtensionsEditor = CreateLabeledTextBox(
            "扩展名匹配",
            state.ExternalLogFileExtensions,
            value => state.ExternalLogFileExtensions = value,
            ServiceConfigUiOptions.ExternalLogFileExtensionsFormatDescription,
            ServiceConfigUiOptions.ExternalLogFileExtensionsPlaceholder);
        logPanel.Children.Add(logModeEditor);
        logPanel.Children.Add(logSizeEditor);
        logPanel.Children.Add(logKeepFilesEditor);
        logPanel.Children.Add(externalLogDirectoryEditor);
        logPanel.Children.Add(externalLogExtensionsEditor);

        BindConditionalVisibility(
            state,
            logModeEditor,
            x => x.LogSourceMode == ServiceLogSourceMode.WinSw,
            nameof(ServiceConfigDraft.LogSourceMode));
        BindConditionalVisibility(
            state,
            logSizeEditor,
            x => x.LogSourceMode == ServiceLogSourceMode.WinSw && IsWinSwRotationMode(x.LogMode),
            nameof(ServiceConfigDraft.LogSourceMode),
            nameof(ServiceConfigDraft.LogMode));
        BindConditionalVisibility(
            state,
            logKeepFilesEditor,
            x => x.LogSourceMode == ServiceLogSourceMode.WinSw && IsWinSwRotationMode(x.LogMode),
            nameof(ServiceConfigDraft.LogSourceMode),
            nameof(ServiceConfigDraft.LogMode));
        BindConditionalVisibility(
            state,
            externalLogDirectoryEditor,
            x => x.LogSourceMode == ServiceLogSourceMode.ExternalFile,
            nameof(ServiceConfigDraft.LogSourceMode));
        BindConditionalVisibility(
            state,
            externalLogExtensionsEditor,
            x => x.LogSourceMode == ServiceLogSourceMode.ExternalFile,
            nameof(ServiceConfigDraft.LogSourceMode));
        tabs.Items.Add(new TabItem { Header = "日志", Content = CreateTabScrollContent(logPanel) });

        var restartPanel = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
        restartPanel.Children.Add(CreateLabeledOptionComboBox(
            "失败动作",
            state.FailureAction,
            ServiceConfigUiOptions.FailureActionOptions,
            value => state.FailureAction = value));
        var restartDelayEditor = CreateLabeledIntTextBox("重启间隔（秒）", state.CrashRestartDelaySeconds, value => state.CrashRestartDelaySeconds = value);
        var resetFailureValueEditor = CreateLabeledIntTextBox("重置失败计数值", state.ResetFailureValue, value => state.ResetFailureValue = value);
        var resetFailureUnitEditor = CreateLabeledOptionComboBox(
            "重置失败计数单位",
            state.ResetFailureUnit,
            ServiceConfigUiOptions.ResetFailureUnitOptions,
            value => state.ResetFailureUnit = value);
        restartPanel.Children.Add(restartDelayEditor);
        restartPanel.Children.Add(resetFailureValueEditor);
        restartPanel.Children.Add(resetFailureUnitEditor);

        BindConditionalVisibility(
            state,
            restartDelayEditor,
            x => x.FailureAction == FailureActionType.Restart,
            nameof(ServiceConfigDraft.FailureAction));
        BindConditionalVisibility(
            state,
            resetFailureValueEditor,
            x => x.FailureAction == FailureActionType.Restart,
            nameof(ServiceConfigDraft.FailureAction));
        BindConditionalVisibility(
            state,
            resetFailureUnitEditor,
            x => x.FailureAction == FailureActionType.Restart,
            nameof(ServiceConfigDraft.FailureAction));
        tabs.Items.Add(new TabItem { Header = "恢复策略", Content = CreateTabScrollContent(restartPanel) });

        var dependencyPanel = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
        dependencyPanel.Children.Add(new TextBlock
        {
            Text = "每行一个服务名，或用逗号/分号分隔。",
            Opacity = 0.7,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });
        dependencyPanel.Children.Add(CreateMultilineTextBox( state.DependenciesText, value => state.DependenciesText = value));
        tabs.Items.Add(new TabItem { Header = "依赖", Content = CreateTabScrollContent(dependencyPanel) });

        var tabArea = new Grid
        {
            Height = ConfigDialogTabAreaHeight,
            MinHeight = ConfigDialogTabAreaHeight,
            MaxHeight = ConfigDialogTabAreaHeight,
            Margin = new Thickness(0, 4, 0, 0)
        };
        tabs.HorizontalAlignment = HorizontalAlignment.Stretch;
        tabs.VerticalAlignment = VerticalAlignment.Stretch;
        tabArea.Children.Add(tabs);
        container.Children.Add(tabArea);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelButton = new Button
        {
            Content = "取消",
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = ConfigDialogAction.Cancel
        };
        cancelButton.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");

        var uninstallButton = new Button
        {
            Content = "卸载服务",
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = ConfigDialogAction.Uninstall
        };
        uninstallButton.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");
        uninstallButton.SetResourceReference(Control.ForegroundProperty, "MaterialDesignValidationErrorBrush");

        var saveButton = new Button
        {
            Content = "保存并应用",
            MinWidth = 96,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = ConfigDialogAction.Save
        };
        saveButton.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");

        actions.Children.Add(cancelButton);
        actions.Children.Add(uninstallButton);
        actions.Children.Add(saveButton);
        container.Children.Add(actions);

        var result = await DialogHost.Show(container, "RootDialogHost").ConfigureAwait(true);
        return result is ConfigDialogAction action ? action : ConfigDialogAction.Cancel;
    }

    private const double ConfigDialogTabAreaHeight = 380;

    private const double ConfigDialogLabelWidth = 140;

    private static ScrollViewer CreateTabScrollContent(UIElement content)
    {
        return new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch
        };
    }



    private static Grid CreateLabeledRow(string label, UIElement editor, Thickness? margin = null)
    {
        var grid = new Grid
        {
            Margin = margin ?? new Thickness(0, 10, 0, 0)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ConfigDialogLabelWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Opacity = 0.78,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(editor, 1);

        if (editor is FrameworkElement fe)
        {
            fe.VerticalAlignment = VerticalAlignment.Center;
        }

        grid.Children.Add(labelBlock);
        grid.Children.Add(editor);
        return grid;
    }

    private static FrameworkElement CreateLabeledTextBox(
        string label,
        string initialValue,
        System.Action<string> onChanged,
        string? formatDescription = null,
        string? placeholder = null)
    {
        var box = new TextBox { Text = initialValue };
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            MaterialDesignThemes.Wpf.HintAssist.SetHint(box, placeholder);
        }

        box.TextChanged += (_, _) => onChanged(box.Text ?? string.Empty);

        if (string.IsNullOrWhiteSpace(formatDescription))
        {
            return CreateLabeledRow(label, box, new Thickness(0, 0, 0, 0));
        }

        var editorPanel = new StackPanel();
        editorPanel.Children.Add(box);
        editorPanel.Children.Add(new TextBlock
        {
            Text = formatDescription,
            Opacity = 0.7,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        return CreateLabeledRow(label, editorPanel, new Thickness(0, 0, 0, 0));
    }

    private static FrameworkElement CreateLabeledIntTextBox(string label, int initialValue, System.Action<int> onChanged)
    {
        return CreateLabeledTextBox(label, initialValue.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                onChanged(parsed);
            }
        });
    }

    private static FrameworkElement CreateMultilineTextBox(string initialValue, System.Action<string> onChanged)
    {
        var box = new TextBox
        {
            Text = initialValue,
            AcceptsReturn = true,
            MinHeight = 120,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        box.TextChanged += (_, _) => onChanged(box.Text ?? string.Empty);
        return box;
    }

    private static FrameworkElement CreateLabeledCheckBox(string label, bool initialValue, System.Action<bool> onChanged)
    {
        var checkBox = new CheckBox
        {
            IsChecked = initialValue,
            VerticalAlignment = VerticalAlignment.Center
        };
        checkBox.Checked += (_, _) => onChanged(true);
        checkBox.Unchecked += (_, _) => onChanged(false);
        return CreateLabeledRow(label, checkBox);
    }

    private static FrameworkElement CreateLabeledOptionComboBox<T>(
        string label,
        T initialValue,
        System.Collections.Generic.IReadOnlyList<DisplayOption<T>> options,
        System.Action<T> onChanged)
    {
        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedValuePath = nameof(DisplayOption<T>.Value),
            DisplayMemberPath = nameof(DisplayOption<T>.Display),
            SelectedValue = initialValue
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedValue is T selected)
            {
                onChanged(selected);
            }
        };

        return CreateLabeledRow(label, combo, new Thickness(0, 0, 0, 0));
    }

    private static bool IsWinSwRotationMode(LogMode mode)
    {
        return mode == LogMode.RollBySize
               || mode == LogMode.RollByTime
               || mode == LogMode.RollBySizeTime;
    }

    private static void BindConditionalVisibility(
        ServiceConfigDraft state,
        FrameworkElement element,
        System.Func<ServiceConfigDraft, bool> condition,
        params string[] watchedProperties)
    {
        void UpdateVisibility()
        {
            element.Visibility = condition(state) ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateVisibility();
        state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == null || watchedProperties.Contains(args.PropertyName))
            {
                UpdateVisibility();
            }
        };
    }

    private enum ConfigDialogAction
    {
        Cancel,
        Save,
        Uninstall
    }

    private async Task ExecuteServiceActionAsync(
        ServiceListItemViewModel? item,
        System.Func<string, Task<Core.Models.OperationResult>> action)
    {
        if (item == null)
        {
            return;
        }

        item.IsBusy = true;
        try
        {
            var result = await action(item.ServiceId).ConfigureAwait(true);
            if (result.Success)
            {
                _snackbarService.ShowSuccess(result.Message);
                var runtime = await _winSwHostService.GetRuntimeInfoAsync(item.ServiceId).ConfigureAwait(true);
                item.UpdateRuntimeInfo(runtime);
            }
            else
            {
                _snackbarService.ShowError(result.Message);
            }
        }
        finally
        {
            item.IsBusy = false;
            await RefreshTrayMonitoringStateAsync().ConfigureAwait(true);
        }
    }

    private async Task ExecuteBatchServiceActionAsync(
        IReadOnlyCollection<ServiceListItemViewModel> items,
        System.Func<string, Task<Core.Models.OperationResult>> action,
        string successMessage)
    {
        if (items.Count == 0)
        {
            _snackbarService.ShowInfo("请先选择至少一项可执行目标。");
            return;
        }

        var failed = 0;
        foreach (var item in items)
        {
            item.IsBusy = true;
            try
            {
                var result = await action(item.ServiceId).ConfigureAwait(true);
                if (!result.Success)
                {
                    failed++;
                    _snackbarService.ShowError(result.Message);
                    continue;
                }

                var runtime = await _winSwHostService.GetRuntimeInfoAsync(item.ServiceId).ConfigureAwait(true);
                item.UpdateRuntimeInfo(runtime);
            }
            finally
            {
                item.IsBusy = false;
            }
        }

        if (failed == 0)
        {
            _snackbarService.ShowSuccess(successMessage);
        }
        else
        {
            _snackbarService.ShowWarning($"批量操作完成，失败 {failed} 项。");
        }

        await RefreshTrayMonitoringStateAsync().ConfigureAwait(true);
    }

    private Task RefreshTrayMonitoringStateAsync()
        => _trayIconService.RefreshMonitoringStateAsync();

    public void SetSelectedServices(IEnumerable<ServiceListItemViewModel> items)
    {
        SelectedServices.Clear();
        foreach (var item in items.Distinct())
        {
            SelectedServices.Add(item);
        }
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        _isUpdatingSelection = true;
        foreach (var item in Services)
        {
            item.IsSelected = value;
        }
        _isUpdatingSelection = false;

        SyncSelectedServicesFromItems();
        UpdateSelectAllState();
    }

    private void ServiceItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ServiceListItemViewModel.IsSelected))
        {
            return;
        }

        if (_isUpdatingSelection)
        {
            return;
        }

        SyncSelectedServicesFromItems();
        UpdateSelectAllState();
    }

    private void SyncSelectedServicesFromItems()
    {
        SelectedServices.Clear();
        foreach (var item in Services.Where(x => x.IsSelected))
        {
            SelectedServices.Add(item);
        }
    }

    private void UpdateSelectAllState()
    {
        _isUpdatingSelection = true;
        if (Services.Count == 0)
        {
            IsAllSelected = false;
            _isUpdatingSelection = false;
            return;
        }

        var selectedCount = Services.Count(x => x.IsSelected);
        IsAllSelected = selectedCount == Services.Count;
        _isUpdatingSelection = false;
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }
}
