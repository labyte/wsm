using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty = true;

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
            Services.Clear();

            foreach (var service in services.OrderBy(x => x.DisplayName))
            {
                var item = new ServiceListItemViewModel(service);
                item.SetConfigFilePath(_paths.GetServiceConfigPath(service.Id));
                Services.Add(item);

                var runtime = await _winSwHostService.GetRuntimeInfoAsync(service.Id).ConfigureAwait(true);
                item.UpdateRuntimeInfo(runtime);
            }
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

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }
}
