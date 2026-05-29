using WSM.App.Shared.Navigation;

namespace WSM.App.Shared.Services;

/// <summary>
/// 页面导航服务。
/// </summary>
public interface INavigationService
{
    void NavigateTo(AppPage page);
}
