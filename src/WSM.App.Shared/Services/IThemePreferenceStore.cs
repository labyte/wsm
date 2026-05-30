namespace WSM.App.Shared.Services;

/// <summary>
/// 主题偏好持久化接口。
/// </summary>
public interface IThemePreferenceStore
{
    /// <summary>
    /// 读取深色主题偏好。
    /// </summary>
    bool? LoadIsDarkTheme();

    /// <summary>
    /// 保存深色主题偏好。
    /// </summary>
    void SaveIsDarkTheme(bool isDarkTheme);
}
