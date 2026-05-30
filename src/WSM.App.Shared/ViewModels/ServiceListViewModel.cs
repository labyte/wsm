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
    private void OpenConfig(ServiceListItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        if (!File.Exists(item.ConfigFilePath))
        {
            _snackbarService.ShowWarning("配置文件不存在，请先安装服务。");
            return;
        }

        OpenPath(item.ConfigFilePath);
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
