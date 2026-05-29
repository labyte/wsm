using System;
using System.IO;
using WSM.Core;

namespace WSM.Infrastructure.Paths;

/// <summary>
/// WSM 数据与 WinSW 部署路径。
/// </summary>
public sealed class WsmPaths
{
    public WsmPaths()
    {
        DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            WsmConstants.DataFolderName);

        WinSwStoreDirectory = Path.Combine(DataRoot, "winsw");
        ServicesDirectory = Path.Combine(DataRoot, "services");
        DatabaseDirectory = Path.Combine(DataRoot, "data");
        DatabasePath = Path.Combine(DatabaseDirectory, "wsm.db");
    }

    /// <summary>
    /// 数据根目录，默认 %ProgramData%\WSM。
    /// </summary>
    public string DataRoot { get; }

    public string WinSwStoreDirectory { get; }

    public string ServicesDirectory { get; }

    public string DatabaseDirectory { get; }

    public string DatabasePath { get; }

    /// <summary>
    /// 应用目录内的 WinSW 源文件（构建时复制到输出目录）。
    /// </summary>
    public string GetBundledWinSwPath(bool preferX64 = true)
    {
        var fileName = preferX64 ? "WinSW-x64.exe" : "WinSW-x86.exe";
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winsw", fileName);
    }

    /// <summary>
    /// 获取指定服务的部署目录。
    /// </summary>
    public string GetServiceDirectory(string serviceId)
    {
        return Path.Combine(ServicesDirectory, serviceId);
    }

    /// <summary>
    /// 获取服务 WinSW 包装器 exe 路径。
    /// </summary>
    public string GetServiceWrapperExePath(string serviceId)
    {
        return Path.Combine(GetServiceDirectory(serviceId), serviceId + ".exe");
    }

    /// <summary>
    /// 获取服务 WinSW XML 配置路径。
    /// </summary>
    public string GetServiceConfigPath(string serviceId)
    {
        return Path.Combine(GetServiceDirectory(serviceId), serviceId + ".xml");
    }

    /// <summary>
    /// 获取服务日志目录。
    /// </summary>
    public string GetServiceLogsDirectory(string serviceId)
    {
        return Path.Combine(GetServiceDirectory(serviceId), "logs");
    }

    /// <summary>
    /// 确保数据根目录及子目录存在。
    /// </summary>
    public void EnsureLayout()
    {
        Directory.CreateDirectory(WinSwStoreDirectory);
        Directory.CreateDirectory(ServicesDirectory);
        Directory.CreateDirectory(DatabaseDirectory);
    }
}
