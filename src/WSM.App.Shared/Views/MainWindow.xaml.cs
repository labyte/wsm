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
    public MainWindow(MainViewModel viewModel, SnackbarMessageQueue messageQueue, ISnackbarService snackbarService)
    {
        InitializeComponent();
        DataContext = viewModel;
        MessageQueue = messageQueue;
        RootSnackbar.MessageQueue = messageQueue;
    }

    public SnackbarMessageQueue MessageQueue { get; }
}
