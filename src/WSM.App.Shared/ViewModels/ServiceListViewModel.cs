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
    private readonly WsmPaths _paths;
    private bool _isUpdatingSelection;

    public ServiceListViewModel(
        IServiceRepository serviceRepository,
        IWinSwHostService winSwHostService,
        ISnackbarService snackbarService,
        WsmPaths paths)
    {
        _serviceRepository = serviceRepository;
        _winSwHostService = winSwHostService;
        _snackbarService = snackbarService;
        _paths = paths;
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
    private async Task RestartAsync(ServiceListItemViewModel? item)
    {
        await ExecuteServiceActionAsync(item, id => _winSwHostService.RestartAsync(id)).ConfigureAwait(true);
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

        if (!await ConfirmUninstallAsync(item).ConfigureAwait(true))
        {
            return;
        }

        item.IsBusy = true;
        try
        {
            var result = await _winSwHostService.UninstallAsync(item.ServiceId).ConfigureAwait(true);
            if (result.Success)
            {
                _snackbarService.ShowSuccess(result.Message);
                Services.Remove(item);
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
        }
    }

    private static async Task<bool> ConfirmUninstallAsync(ServiceListItemViewModel item)
    {
        var contentPanel = new StackPanel
        {
            MinWidth = 360,
            Margin = new Thickness(24)
        };

        contentPanel.Children.Add(new TextBlock
        {
            Text = "确认卸载",
            FontSize = 18,
            FontWeight = FontWeights.Medium
        });

        contentPanel.Children.Add(new TextBlock
        {
            Text = $"确认卸载服务“{item.DisplayName}”吗？\n此操作会移除该服务及其相关部署内容。",
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

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
            CommandParameter = false
        };
        cancelButton.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");

        var confirmButton = new Button
        {
            Content = "确认卸载",
            MinWidth = 88,
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = true
        };
        confirmButton.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");
        confirmButton.SetResourceReference(Control.ForegroundProperty, "MaterialDesignValidationErrorBrush");

        actions.Children.Add(cancelButton);
        actions.Children.Add(confirmButton);
        contentPanel.Children.Add(actions);

        var result = await DialogHost.Show(contentPanel, "RootDialogHost").ConfigureAwait(true);
        return result is bool confirmed && confirmed;
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
            MinWidth = 520
        };

        container.Children.Add(new TextBlock
        {
            Text = $"服务配置 - {state.Id}",
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

        container.Children.Add(CreateLabeledTextBox("显示名称", state.DisplayName, value => state.DisplayName = value));
        container.Children.Add(CreateLabeledTextBox("描述", state.Description, value => state.Description = value));
        container.Children.Add(CreateLabeledTextBox("可执行路径", state.ExecutablePath, value => state.ExecutablePath = value));
        container.Children.Add(CreateLabeledTextBox("工作目录", state.WorkingDirectory, value => state.WorkingDirectory = value));
        container.Children.Add(CreateLabeledTextBox("启动参数", state.Arguments, value => state.Arguments = value));
        container.Children.Add(CreateLabeledComboBox("启动类型", state.StartMode, value => state.StartMode = value));
        container.Children.Add(CreateLabeledCheckBox("启用延迟自动启动", state.DelayedAutoStart, value => state.DelayedAutoStart = value));
        container.Children.Add(CreateLabeledCheckBox("启用自动刷新配置（autoRefresh）", state.AutoRefresh, value => state.AutoRefresh = value));
        container.Children.Add(CreateLabeledCheckBox("隐藏程序窗口（hidewindow）", state.HideWindow, value => state.HideWindow = value));
        container.Children.Add(CreateLabeledIntTextBox("停止超时（秒）", state.StopTimeoutSeconds, value => state.StopTimeoutSeconds = value));
        container.Children.Add(CreateLabeledCheckBox("安装后自动启动", state.StartAfterInstall, value => state.StartAfterInstall = value));
        container.Children.Add(new TextBlock
        {
            Text = "自动恢复设置",
            Margin = new Thickness(0, 8, 0, 6),
            FontWeight = FontWeights.Medium
        });
        container.Children.Add(CreateLabeledCheckBox("启用崩溃自动恢复", state.EnableCrashRecovery, value => state.EnableCrashRecovery = value));
        container.Children.Add(CreateLabeledIntTextBox("崩溃重启延迟（秒）", state.CrashRestartDelaySeconds, value => state.CrashRestartDelaySeconds = value));
        container.Children.Add(CreateLabeledIntTextBox("最大重启次数", state.CrashMaxRestartCount, value => state.CrashMaxRestartCount = value));

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

    private static FrameworkElement CreateLabeledTextBox(string label, string initialValue, System.Action<string> onChanged)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock { Text = label, Opacity = 0.78 });
        var box = new TextBox { Text = initialValue };
        box.TextChanged += (_, _) => onChanged(box.Text ?? string.Empty);
        panel.Children.Add(box);
        return panel;
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

    private static FrameworkElement CreateLabeledCheckBox(string label, bool initialValue, System.Action<bool> onChanged)
    {
        var checkBox = new CheckBox
        {
            Content = label,
            IsChecked = initialValue,
            Margin = new Thickness(0, 0, 0, 8)
        };
        checkBox.Checked += (_, _) => onChanged(true);
        checkBox.Unchecked += (_, _) => onChanged(false);
        return checkBox;
    }

    private static FrameworkElement CreateLabeledComboBox(
        string label,
        ManagedServiceStartMode initialValue,
        System.Action<ManagedServiceStartMode> onChanged)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock { Text = label, Opacity = 0.78 });

        var combo = new ComboBox
        {
            ItemsSource = System.Enum.GetValues(typeof(ManagedServiceStartMode)),
            SelectedItem = initialValue
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ManagedServiceStartMode selected)
            {
                onChanged(selected);
            }
        };

        panel.Children.Add(combo);
        return panel;
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
    }

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
