using System.Windows;
using System.Windows.Controls;

namespace WSM.App.Shared.Views.Controls;

/// <summary>
/// 控制台风格日志面板，支持文本选择与复制。
/// </summary>
public partial class ConsoleLogView : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(ConsoleLogView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.Register(
        nameof(AutoScroll),
        typeof(bool),
        typeof(ConsoleLogView),
        new PropertyMetadata(true, OnAutoScrollChanged));

    public static readonly DependencyProperty WrapLinesProperty = DependencyProperty.Register(
        nameof(WrapLines),
        typeof(bool),
        typeof(ConsoleLogView),
        new PropertyMetadata(true));

    public ConsoleLogView()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool AutoScroll
    {
        get => (bool)GetValue(AutoScrollProperty);
        set => SetValue(AutoScrollProperty, value);
    }

    public bool WrapLines
    {
        get => (bool)GetValue(WrapLinesProperty);
        set => SetValue(WrapLinesProperty, value);
    }

    public void SelectAll()
    {
        LogTextBox.Focus();
        LogTextBox.SelectAll();
    }

    private void OnLogTextChanged(object sender, TextChangedEventArgs e)
    {
        if (AutoScroll)
        {
            LogTextBox.ScrollToEnd();
        }
    }

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ConsoleLogView view && e.NewValue is bool enabled && enabled)
        {
            view.LogTextBox.ScrollToEnd();
        }
    }
}
