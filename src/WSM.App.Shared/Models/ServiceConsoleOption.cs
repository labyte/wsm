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
    /// null 表示全部服务。
    /// </summary>
    public string? ServiceId { get; }

    public string DisplayName { get; }

    public static ServiceConsoleOption All { get; } = new ServiceConsoleOption(null, "全部服务");
}
