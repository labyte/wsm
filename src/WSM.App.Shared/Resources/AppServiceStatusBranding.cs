using System;
using System.Windows;
using System.Windows.Media;

namespace WSM.App.Shared.Resources;

/// <summary>
/// 服务列表状态列与操作列颜色（随浅色/深色主题切换）。
/// </summary>
public static class AppServiceStatusBranding
{
    public const string RunningKey = "ServiceStatusRunningForeground";
    public const string StoppedKey = "ServiceStatusStoppedForeground";
    public const string PendingKey = "ServiceStatusPendingForeground";
    public const string NeutralKey = "ServiceStatusNeutralForeground";
    public const string ActionDangerKey = "ServiceActionDangerForeground";
    public const string ActionSecondaryKey = "ServiceActionSecondaryForeground";

    public static void Apply(bool isDarkTheme)
    {
        var resources = Application.Current.Resources;
        resources[RunningKey] = CreateBrush(isDarkTheme ? "#66BB6A" : "#2E7D32");
        resources[StoppedKey] = CreateBrush(isDarkTheme ? "#EF5350" : "#B71C1C");
        resources[PendingKey] = CreateBrush(isDarkTheme ? "#FFB74D" : "#EF6C00");
        resources[NeutralKey] = CreateBrush(isDarkTheme ? "#90A4AE" : "#616161");
        resources[ActionDangerKey] = CreateBrush(isDarkTheme ? "#FF8A80" : "#C62828");
        resources[ActionSecondaryKey] = CreateBrush(isDarkTheme ? "#ECEFF1" : "#424242");
    }

    private static SolidColorBrush CreateBrush(string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex)!;
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}
