using System;
using System.IO;

namespace WSM.App.Shared.Services;

/// <summary>
/// 基于本地文件的主窗口关闭行为偏好持久化实现。
/// </summary>
public sealed class CloseWindowPreferenceStore : ICloseWindowPreferenceStore
{
    private readonly string _configPath;

    public CloseWindowPreferenceStore()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WSM");
        Directory.CreateDirectory(appData);
        _configPath = Path.Combine(appData, "close-window-preference.txt");
    }

    public bool? LoadMinimizeOnClose()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return null;
            }

            var content = File.ReadAllText(_configPath).Trim();
            if (bool.TryParse(content, out var minimizeOnClose))
            {
                return minimizeOnClose;
            }
        }
        catch
        {
            // 忽略配置读取异常，回退到默认行为。
        }

        return null;
    }

    public void SaveMinimizeOnClose(bool minimizeOnClose)
    {
        try
        {
            File.WriteAllText(_configPath, minimizeOnClose.ToString());
        }
        catch
        {
            // 忽略配置写入异常，不影响主流程。
        }
    }
}
