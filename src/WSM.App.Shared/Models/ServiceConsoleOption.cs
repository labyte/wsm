namespace WSM.App.Shared.Models;

/// <summary>
/// 服务控制台服务选择项。
/// </summary>
public sealed class ServiceConsoleOption
{
    public ServiceConsoleOption(string? serviceId, string displayName)
    {
        ServiceId = serviceId;
        DisplayName = displayName;
    }

    /// <summary>
    /// null 表示聚合视图（全部服务或综合日志）。
    /// </summary>
    public string? ServiceId { get; }

    public string DisplayName { get; }

    public static ServiceConsoleOption All { get; } = new ServiceConsoleOption(null, "全部服务");

    /// <summary>
    /// 日志页综合视图：读取 WSM operations.log。
    /// </summary>
    public static ServiceConsoleOption Combined { get; } = new ServiceConsoleOption(null, "综合");
}
