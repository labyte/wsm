using System;
using System.IO;

namespace WSM.App.Shared.Services;

/// <summary>
/// 基于本地文件的主题偏好持久化实现。
/// </summary>
public sealed class ThemePreferenceStore : IThemePreferenceStore
{
    private readonly string _configPath;

    public ThemePreferenceStore()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WSM");
        Directory.CreateDirectory(appData);
        _configPath = Path.Combine(appData, "theme-preference.txt");
    }

    public bool? LoadIsDarkTheme()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return null;
            }

            var content = File.ReadAllText(_configPath).Trim();
            if (bool.TryParse(content, out var isDarkTheme))
            {
                return isDarkTheme;
            }
        }
        catch
        {
            // 忽略配置读取异常，回退到默认主题。
        }

        return null;
    }

    public void SaveIsDarkTheme(bool isDarkTheme)
    {
        try
        {
            File.WriteAllText(_configPath, isDarkTheme.ToString());
        }
        catch
        {
            // 忽略配置写入异常，不影响主流程。
        }
    }
}
