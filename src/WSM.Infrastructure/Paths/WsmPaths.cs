using System;
using System.IO;
using WSM.Core;

namespace WSM.Infrastructure.Paths;

/// <summary>
/// WSM 数据与 WinSW 部署路径。
/// </summary>
public sealed class WsmPaths
{
    private readonly object _sync = new object();
    private string _dataRoot = string.Empty;
    private string _winSwStoreDirectory = string.Empty;
    private string _servicesDirectory = string.Empty;
    private string _databaseDirectory = string.Empty;
    private string _databasePath = string.Empty;

    public WsmPaths()
    {
        var defaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            WsmConstants.DataFolderName);
        var configuredRoot = LoadConfiguredDataRoot();
        var initialRoot = string.IsNullOrWhiteSpace(configuredRoot) ? defaultRoot : configuredRoot!;
        UpdateDerivedPaths(initialRoot);
    }

    /// <summary>
    /// 数据根目录，默认 %ProgramData%\WSM。
    /// </summary>
    public string DataRoot => _dataRoot;

    public string WinSwStoreDirectory => _winSwStoreDirectory;

    public string ServicesDirectory => _servicesDirectory;

    public string DatabaseDirectory => _databaseDirectory;

    public string DatabasePath => _databasePath;

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

    /// <summary>
    /// 更新并持久化数据目录。
    /// </summary>
    public bool SetDataRoot(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            throw new ArgumentException("数据目录不能为空。", nameof(dataRoot));
        }

        var normalized = Path.GetFullPath(dataRoot.Trim());
        lock (_sync)
        {
            if (string.Equals(_dataRoot, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            UpdateDerivedPaths(normalized);
            SaveConfiguredDataRoot(normalized);
        }

        EnsureLayout();
        return true;
    }

    private void UpdateDerivedPaths(string dataRoot)
    {
        _dataRoot = dataRoot;
        _winSwStoreDirectory = Path.Combine(_dataRoot, "winsw");
        _servicesDirectory = Path.Combine(_dataRoot, "services");
        _databaseDirectory = Path.Combine(_dataRoot, "data");
        _databasePath = Path.Combine(_databaseDirectory, "wsm.db");
    }

    private static string GetConfigFilePath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WSM");
        Directory.CreateDirectory(appData);
        return Path.Combine(appData, "data-root.txt");
    }

    private static string? LoadConfiguredDataRoot()
    {
        var configPath = GetConfigFilePath();
        if (!File.Exists(configPath))
        {
            return null;
        }

        var content = File.ReadAllText(configPath).Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    private static void SaveConfiguredDataRoot(string dataRoot)
    {
        var configPath = GetConfigFilePath();
        File.WriteAllText(configPath, dataRoot);
    }
}
