namespace WSM.Core.Models;

/// <summary>
/// 环境变量键值对。
/// </summary>
public sealed class EnvVariable
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
