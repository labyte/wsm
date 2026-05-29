using System.Windows;
using MaterialDesignThemes.Wpf;
using WSM.App.Shared.Services;
using WSM.App.Shared.ViewModels;

namespace WSM.App.Shared.Views;

/// <summary>
/// 共享主窗口。
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISnackbarService _snackbarService;

    public MainWindow(MainViewModel viewModel, SnackbarMessageQueue messageQueue, ISnackbarService snackbarService)
    {
        InitializeComponent();
        DataContext = viewModel;
        MessageQueue = messageQueue;
        RootSnackbar.MessageQueue = messageQueue;
        _snackbarService = snackbarService;
    }

    public SnackbarMessageQueue MessageQueue { get; }

    private void OnTestSnackbarClick(object sender, RoutedEventArgs e)
    {
        _snackbarService.ShowSuccess("Snackbar 冒泡提示工作正常（非 MessageBox）");
    }
}
