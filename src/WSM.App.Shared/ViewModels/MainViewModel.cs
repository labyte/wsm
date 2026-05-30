using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WSM.App.Shared.Models;
using WSM.App.Shared.Navigation;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 主窗口 ViewModel。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ServiceListViewModel _serviceListViewModel;
    private readonly ServiceInstallViewModel _serviceInstallViewModel;
    private readonly ServiceConsoleViewModel _serviceConsoleViewModel;
    private readonly LogViewerViewModel _logViewerViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    public MainViewModel(
        ServiceListViewModel serviceListViewModel,
        ServiceInstallViewModel serviceInstallViewModel,
        ServiceConsoleViewModel serviceConsoleViewModel,
        LogViewerViewModel logViewerViewModel,
        SettingsViewModel settingsViewModel)
    {
        _serviceListViewModel = serviceListViewModel;
        _serviceInstallViewModel = serviceInstallViewModel;
        _serviceConsoleViewModel = serviceConsoleViewModel;
        _logViewerViewModel = logViewerViewModel;
        _settingsViewModel = settingsViewModel;

        PrimaryNavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem(AppPage.ServiceList, "服务", "FormatListBulletedSquare"),
            new NavigationItem(AppPage.ServiceConsole, "控制台", "ConsoleLine"),
            new NavigationItem(AppPage.Logs, "日志", "TextBoxSearchOutline"),
            new NavigationItem(AppPage.AddService, "添加", "PlusBox"),
            new NavigationItem(AppPage.Settings, "设置", "CogOutline")
        };

        SelectedNavigationItem = PrimaryNavigationItems.First();
    }

    public string Title => Core.WsmConstants.AppDisplayName;

    public ObservableCollection<NavigationItem> PrimaryNavigationItems { get; }

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    private string _selectedPageTitle = "服务总览";

    [ObservableProperty]
    private object? _currentPageViewModel;

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value != null)
        {
            NavigateTo(value);
        }
    }

    public void NavigateTo(AppPage page)
    {
        var item = PrimaryNavigationItems.FirstOrDefault(x => x.Page == page);
        if (item == null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedNavigationItem, item))
        {
            SelectedNavigationItem = item;
            return;
        }

        NavigateTo(item);
    }

    private void NavigateTo(NavigationItem item)
    {
        SelectedPageTitle = item.Title;

        switch (item.Page)
        {
            case AppPage.ServiceList:
                CurrentPageViewModel = _serviceListViewModel;
                break;
            case AppPage.AddService:
                CurrentPageViewModel = _serviceInstallViewModel;
                break;
            case AppPage.ServiceConsole:
                CurrentPageViewModel = _serviceConsoleViewModel;
                break;
            case AppPage.Logs:
                CurrentPageViewModel = _logViewerViewModel;
                break;
            case AppPage.Settings:
                CurrentPageViewModel = _settingsViewModel;
                break;
        }

        if (CurrentPageViewModel is INavigationAware aware)
        {
            aware.OnNavigatedTo();
        }
    }
}
