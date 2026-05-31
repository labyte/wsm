namespace WSM.Core.Models;

/// <summary>
/// 服务日志来源模式。
/// </summary>
public enum ServiceLogSourceMode
{
    /// <summary>
    /// 使用 WinSW 生成并管理 stdout/stderr 日志。
    /// </summary>
    WinSw,

    /// <summary>
    /// 外部日志文件
    /// </summary>
    ExternalFile
}
