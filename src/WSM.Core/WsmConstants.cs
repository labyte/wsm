namespace WSM.Core;

/// <summary>
/// 应用程序常量。
/// </summary>
public static class WsmConstants
{
    public const string AppName = "WSM";
    public const string AppDisplayName = "Windows Service Manager";
    public const string DataFolderName = "WSM";

    /// <summary>
    /// 托管服务 WinSW 日志子目录名（相对服务部署目录）。
    /// </summary>
    public const string ServiceLogsSubdirectoryName = "logs";

    /// <summary>
    /// WinSW logpath 表达式：始终相对 wrapper/xml 所在目录（%BASE%），不受 workingdirectory 影响。
    /// </summary>
    public const string ServiceWinSwLogPath = "%BASE%\\logs";
}
