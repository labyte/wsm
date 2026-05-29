using System.Collections.Generic;

namespace WSM.Core.Models;

/// <summary>
/// 单次失败动作配置（对应 WinSW onfailure 元素）。
/// </summary>
public sealed class FailureActionEntry
{
    public FailureActionType Action { get; set; } = FailureActionType.Restart;

    /// <summary>
    /// WinSW 延迟格式，如 "5 sec"、"10 sec"。
    /// </summary>
    public string Delay { get; set; } = "5 sec";
}
