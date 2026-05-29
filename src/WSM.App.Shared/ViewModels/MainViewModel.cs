using CommunityToolkit.Mvvm.ComponentModel;
using WSM.Core;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 主窗口 ViewModel。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public string Title => WsmConstants.AppDisplayName;

    [ObservableProperty]
    private string _subtitle = "就绪 — 优先在 Win10 Modern 版调试";

    [ObservableProperty]
    private string _selectedPage = "服务总览";
}
