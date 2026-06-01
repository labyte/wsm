using System.Linq;
using WSM.App.Shared.ViewModels;
using WSM.Core.Models;
using WSM.Infrastructure.Logging;

namespace WSM.App.Shared.Services;

/// <summary>
/// 将 <see cref="ServiceLogSourceInfo"/> 格式化为状态栏文案。
/// </summary>
public static class ServiceLogSourceStatusFormatter
{
    public static string FormatSchemeAndPath(ServiceLogSourceInfo info, string? viewHint = null)
    {
        var scheme = FormatScheme(info);
        if (!string.IsNullOrWhiteSpace(viewHint))
        {
            scheme = $"{scheme} · {viewHint}";
        }

        return $"{scheme} · {FormatPath(info)}";
    }

    /// <summary>
    /// 简短日志方案描述（供状态栏展示）。
    /// </summary>
    public static string FormatScheme(ServiceLogSourceInfo info)
    {
        if (info.SourceMode == ServiceLogSourceMode.ExternalFile)
        {
            return "外部日志";
        }

        if (info.WinSwLogMode == null)
        {
            return "WinSW";
        }

        var modeDisplay = ServiceConfigUiOptions.WinSwLogModeOptions
            .FirstOrDefault(x => x.Value == info.WinSwLogMode.Value)
            ?.Display ?? info.WinSwLogMode.Value.ToString();
        return $"WinSW · {modeDisplay}";
    }

    /// <summary>
    /// 日志路径摘要（供状态栏展示）。
    /// </summary>
    public static string FormatPath(ServiceLogSourceInfo info)
    {
        return string.IsNullOrWhiteSpace(info.PrimaryPath) ? "—" : info.PrimaryPath;
    }
}
