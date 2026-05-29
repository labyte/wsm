using WSM.App.Shared.Navigation;

namespace WSM.App.Shared.Models;

/// <summary>
/// 侧边栏导航项。
/// </summary>
public sealed class NavigationItem
{
    public NavigationItem(AppPage page, string title, string iconKind)
    {
        Page = page;
        Title = title;
        IconKind = iconKind;
    }

    public AppPage Page { get; }

    public string Title { get; }

    public string IconKind { get; }
}
