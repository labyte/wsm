namespace WSM.Core.Models;

/// <summary>
/// 进程指标快照（Modern 版可选）。
/// </summary>
public sealed class ProcessMetrics
{
    public string ServiceId { get; set; } = string.Empty;

    public double CpuPercent { get; set; }

    public long WorkingSetBytes { get; set; }

    public System.DateTime SampledAtUtc { get; set; } = System.DateTime.UtcNow;
}
