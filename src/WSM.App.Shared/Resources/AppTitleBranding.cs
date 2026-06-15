using System;
using System.Windows;
using System.Windows.Media;

namespace WSM.App.Shared.Resources;

/// <summary>
/// 主窗口标题品牌字渐变色。
/// </summary>
public static class AppTitleBranding
{
    private static readonly LinearGradientBrush LightTitleForeground = CreateHorizontalGradient("#309CF0", "#3962BD");
    private static readonly LinearGradientBrush DarkTitleForeground = CreateHorizontalGradient("#FFFFFF", "#CECECD");

    public static Brush GetTitleForeground(bool isDarkTheme)
        => isDarkTheme ? DarkTitleForeground : LightTitleForeground;

    private static LinearGradientBrush CreateHorizontalGradient(string startHex, string endHex)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        brush.GradientStops.Add(new GradientStop(ParseColor(startHex), 0));
        brush.GradientStops.Add(new GradientStop(ParseColor(endHex), 1));
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string hex)
        => (Color)ColorConverter.ConvertFromString(hex)!;
}
