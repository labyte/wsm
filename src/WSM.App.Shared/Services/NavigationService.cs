using System;
using Microsoft.Extensions.DependencyInjection;
using WSM.App.Shared.Navigation;
using WSM.App.Shared.ViewModels;

namespace WSM.App.Shared.Services;

/// <summary>
/// 页面导航服务，延迟解析 MainViewModel 以避免 DI 循环依赖。
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private MainViewModel? _mainViewModel;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo(AppPage page)
    {
        _mainViewModel ??= _serviceProvider.GetRequiredService<MainViewModel>();
        _mainViewModel.NavigateTo(page);
    }
}
