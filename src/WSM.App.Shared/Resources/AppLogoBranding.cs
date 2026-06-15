using System;

namespace WSM.App.Shared.Resources;

/// <summary>
/// 应用 Logo 资源路径。
/// </summary>
public static class AppLogoBranding
{
    private const string PackBase = "pack://application:,,,/WSM.App.Shared;component/Assets/";

    public static Uri GetHeaderLogoUri(bool isDarkTheme)
        => new Uri(PackBase + (isDarkTheme ? "logo-dark.png" : "logo-light.png"), UriKind.Absolute);
}
