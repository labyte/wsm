namespace WSM.App.Shared.Services;

/// <summary>
/// 主窗口关闭行为偏好持久化接口。
/// </summary>
public interface ICloseWindowPreferenceStore
{
    /// <summary>
    /// 读取关闭主窗口时是否最小化到托盘；未配置时返回 null。
    /// </summary>
    bool? LoadMinimizeOnClose();

    /// <summary>
    /// 保存关闭主窗口时是否最小化到托盘。
    /// </summary>
    void SaveMinimizeOnClose(bool minimizeOnClose);
}
