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
            scheme = $"{scheme}·{viewHint}";
        }

        var path = string.IsNullOrWhiteSpace(info.PrimaryPath)
            ? "（路径不可用）"
            : info.PrimaryPath;
        return $"{scheme} · {path}";
    }

    private static string FormatScheme(ServiceLogSourceInfo info)
    {
        if (info.SourceMode == ServiceLogSourceMode.ExternalFile)
        {
            return GetLogSourceDisplay(ServiceLogSourceMode.ExternalFile);
        }

        var winSwScheme = GetLogSourceDisplay(ServiceLogSourceMode.WinSw);
        if (info.WinSwLogMode == null)
        {
            return winSwScheme;
        }

        var modeDisplay = ServiceConfigUiOptions.WinSwLogModeOptions
            .FirstOrDefault(x => x.Value == info.WinSwLogMode.Value)
            ?.Display ?? info.WinSwLogMode.Value.ToString();
        return $"{winSwScheme}·{modeDisplay}";
    }

    private static string GetLogSourceDisplay(ServiceLogSourceMode mode) =>
        ServiceConfigUiOptions.LogSourceOptions
            .FirstOrDefault(x => x.Value == mode)
            ?.Display ?? mode.ToString();
}
