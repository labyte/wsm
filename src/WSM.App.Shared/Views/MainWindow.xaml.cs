using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using WSM.App.Shared.Services;
using WSM.App.Shared.ViewModels;

namespace WSM.App.Shared.Views;

/// <summary>
/// 共享主窗口。
/// </summary>
public partial class MainWindow : Window
{
    private const double WideLayoutBreakpoint = 960;
    private readonly PaletteHelper _paletteHelper = new();
    private readonly IThemeManager? _themeManager;
    private readonly IThemePreferenceStore _themePreferenceStore;
    private bool _isWideLayout;
    private bool _isLayoutInitialized;

    public MainWindow(
        MainViewModel viewModel,
        SnackbarMessageQueue messageQueue,
        ISnackbarService snackbarService,
        IThemePreferenceStore themePreferenceStore)
    {
        InitializeComponent();
        DataContext = viewModel;
        MessageQueue = messageQueue;
        RootSnackbar.MessageQueue = messageQueue;
        _themePreferenceStore = themePreferenceStore;
        _themeManager = _paletteHelper.GetThemeManager();
        if (_themeManager != null)
        {
            _themeManager.ThemeChanged += (_, e) =>
            {
                if (e.NewTheme != null)
                {
                    DarkModeToggleButton.IsChecked = e.NewTheme.GetBaseTheme() == BaseTheme.Dark;
                }
            };
        }

        var savedTheme = _themePreferenceStore.LoadIsDarkTheme();
        if (savedTheme.HasValue)
        {
            ApplyTheme(savedTheme.Value, persist: false);
        }
        else
        {
            SyncThemeToggleState();
        }
    }

    public SnackbarMessageQueue MessageQueue { get; }

    private void MenuDarkModeButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(DarkModeToggleButton.IsChecked == true, persist: true);
    }

    private void SyncThemeToggleState()
    {
        var theme = _paletteHelper.GetTheme();
        DarkModeToggleButton.IsChecked = theme.GetBaseTheme() == BaseTheme.Dark;
    }

    private void ApplyTheme(bool isDarkTheme, bool persist)
    {
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);
        DarkModeToggleButton.IsChecked = isDarkTheme;

        if (persist)
        {
            _themePreferenceStore.SaveIsDarkTheme(isDarkTheme);
        }
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateResponsiveLayout(ActualWidth);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width);
    }

    private void MenuToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isWideLayout)
        {
            MenuToggleButton.IsChecked = false;
        }
    }

    private void RailMenuButton_OnClick(object sender, RoutedEventArgs e)
    {
        MenuToggleButton.IsChecked = true;
    }

    private void NavigationList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavDrawer.OpenMode == DrawerHostOpenMode.Modal && NavDrawer.IsLeftDrawerOpen)
        {
            MenuToggleButton.IsChecked = false;
        }
    }

    private void UpdateResponsiveLayout(double windowWidth)
    {
        var shouldUseWideLayout = windowWidth >= WideLayoutBreakpoint;
        if (!_isLayoutInitialized)
        {
            _isLayoutInitialized = true;
            _isWideLayout = shouldUseWideLayout;
            ApplyLayoutState(shouldUseWideLayout, forceCloseOnCompact: !shouldUseWideLayout);
            return;
        }

        if (_isWideLayout == shouldUseWideLayout)
        {
            return;
        }

        _isWideLayout = shouldUseWideLayout;
        ApplyLayoutState(shouldUseWideLayout, forceCloseOnCompact: !shouldUseWideLayout);
    }

    private void ApplyLayoutState(bool isWideLayout, bool forceCloseOnCompact)
    {
        if (isWideLayout)
        {
            NavDrawer.OpenMode = DrawerHostOpenMode.Modal;
            MenuToggleButton.IsChecked = false;
            NavRailPanel.Visibility = Visibility.Visible;
            MenuToggleButton.Visibility = Visibility.Collapsed;
            return;
        }

        NavDrawer.OpenMode = DrawerHostOpenMode.Modal;
        NavRailPanel.Visibility = Visibility.Collapsed;
        MenuToggleButton.Visibility = Visibility.Visible;
        if (forceCloseOnCompact)
        {
            MenuToggleButton.IsChecked = false;
        }
    }
}
