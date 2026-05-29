using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WSM.App.Shared.Converters;

/// <summary>
/// 布尔值反转可见性转换器。
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
