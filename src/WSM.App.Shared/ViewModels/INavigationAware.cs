namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 页面 ViewModel 标记接口，用于导航后刷新。
/// </summary>
public interface INavigationAware
{
    void OnNavigatedTo();
}
