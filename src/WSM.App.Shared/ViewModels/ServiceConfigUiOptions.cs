using System.Collections.Generic;
using WSM.Core.Models;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 服务配置页面的可视化选项定义（中文显示）。
/// </summary>
public static class ServiceConfigUiOptions
{
    public static readonly IReadOnlyList<DisplayOption<ManagedServiceStartMode>> StartModeOptions =
        new List<DisplayOption<ManagedServiceStartMode>>
        {
            new DisplayOption<ManagedServiceStartMode>(ManagedServiceStartMode.Automatic, "自动"),
            new DisplayOption<ManagedServiceStartMode>(ManagedServiceStartMode.Manual, "手动"),
            new DisplayOption<ManagedServiceStartMode>(ManagedServiceStartMode.Disabled, "禁用")
        };

    public static readonly IReadOnlyList<DisplayOption<ServiceLogSourceMode>> LogSourceOptions =
        new List<DisplayOption<ServiceLogSourceMode>>
        {
            new DisplayOption<ServiceLogSourceMode>(ServiceLogSourceMode.WinSw, "管理器提供（WinSW）"),
            new DisplayOption<ServiceLogSourceMode>(ServiceLogSourceMode.ExternalFile, "外部日志")
        };

    public static readonly IReadOnlyList<DisplayOption<LogMode>> WinSwLogModeOptions =
        new List<DisplayOption<LogMode>>
        {
            new DisplayOption<LogMode>(LogMode.Append, "追加"),
            new DisplayOption<LogMode>(LogMode.Reset, "每次启动清空"),
            new DisplayOption<LogMode>(LogMode.Ignore, "不输出"),
            new DisplayOption<LogMode>(LogMode.RollBySize, "按大小滚动"),
            new DisplayOption<LogMode>(LogMode.RollByTime, "按时间滚动"),
            new DisplayOption<LogMode>(LogMode.RollBySizeTime, "按大小+时间滚动")
        };

    public static readonly IReadOnlyList<DisplayOption<FailureActionType>> FailureActionOptions =
        new List<DisplayOption<FailureActionType>>
        {
            new DisplayOption<FailureActionType>(FailureActionType.Restart, "重启服务"),
            new DisplayOption<FailureActionType>(FailureActionType.None, "不处理")
        };

    public static readonly IReadOnlyList<DisplayOption<string>> ResetFailureUnitOptions =
        new List<DisplayOption<string>>
        {
            new DisplayOption<string>("minute", "分钟"),
            new DisplayOption<string>("hour", "小时"),
            new DisplayOption<string>("day", "天"),
            new DisplayOption<string>("month", "月")
        };
}

/// <summary>
/// 通用显示选项（值+显示文本）。
/// </summary>
public sealed class DisplayOption<T>
{
    public DisplayOption(T value, string display)
    {
        Value = value;
        Display = display;
    }

    public T Value { get; }
    public string Display { get; }
}
