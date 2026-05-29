using System.Collections.Generic;

namespace WSM.Core.Models;

/// <summary>
/// 服务失败重启策略。
/// </summary>
public sealed class FailurePolicy
{
    public IList<FailureActionEntry> Actions { get; set; } = new List<FailureActionEntry>();

    /// <summary>
    /// 失败计数重置周期，WinSW 格式如 "1 hour"。
    /// </summary>
    public string ResetFailurePeriod { get; set; } = "1 hour";

    public static FailurePolicy CreateStandard()
    {
        return CreateFromTemplate(FailurePolicyTemplate.Standard);
    }

    public static FailurePolicy CreateFromTemplate(FailurePolicyTemplate template)
    {
        switch (template)
        {
            case FailurePolicyTemplate.Aggressive:
                return new FailurePolicy
                {
                    ResetFailurePeriod = "30 min",
                    Actions =
                    {
                        new FailureActionEntry { Action = FailureActionType.Restart, Delay = "3 sec" },
                        new FailureActionEntry { Action = FailureActionType.Restart, Delay = "5 sec" },
                        new FailureActionEntry { Action = FailureActionType.Restart, Delay = "10 sec" }
                    }
                };

            case FailurePolicyTemplate.MonitorOnly:
                return new FailurePolicy
                {
                    ResetFailurePeriod = "1 hour",
                    Actions =
                    {
                        new FailureActionEntry { Action = FailureActionType.None, Delay = "0 sec" }
                    }
                };

            default:
                return new FailurePolicy
                {
                    ResetFailurePeriod = "1 hour",
                    Actions =
                    {
                        new FailureActionEntry { Action = FailureActionType.Restart, Delay = "5 sec" },
                        new FailureActionEntry { Action = FailureActionType.Restart, Delay = "10 sec" },
                        new FailureActionEntry { Action = FailureActionType.None, Delay = "0 sec" }
                    }
                };
        }
    }
}
