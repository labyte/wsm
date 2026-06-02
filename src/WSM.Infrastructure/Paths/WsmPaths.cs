using System;
using System.IO;
using WSM.Core;

namespace WSM.Infrastructure.Paths;

/// <summary>
/// WSM 数据与 WinSW 部署路径。
/// </summary>
public sealed class WsmPaths
{
    public const string DefaultRuleProgramName = "ProgramName";
    public const string DefaultRulePrefixProgramName = "PrefixProgramName";

    private readonly object _sync = new object();
    private string _dataRoot = string.Empty;
    private string _winSwStoreDirectory = string.Empty;
    private string _servicesDirectory = string.Empty;
    private string _databaseDirectory = string.Empty;
    private string _databasePath = string.Empty;
    private string _operationLogPath = string.Empty;
    private string _serviceIdRuleMode = DefaultRuleProgramName;
    private string _serviceIdRulePrefix = "svc-";
    private string _serviceNameRuleMode = DefaultRuleProgramName;
    private string _serviceNameRulePrefix = string.Empty;
    private string _serviceDescriptionRuleMode = DefaultRulePrefixProgramName;
    private string _serviceDescriptionRulePrefix = "由 WSM 托管：";

    public WsmPaths()
    {
        var defaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            WsmConstants.DataFolderName);
        var configuredRoot = LoadConfiguredDataRoot();
        var initialRoot = string.IsNullOrWhiteSpace(configuredRoot) ? defaultRoot : configuredRoot!;
        UpdateDerivedPaths(initialRoot);

        var configuredNaming = LoadConfiguredServiceNamingRules();
        if (!string.IsNullOrWhiteSpace(configuredNaming.IdRuleMode))
        {
            _serviceIdRuleMode = configuredNaming.IdRuleMode!;
        }

        if (configuredNaming.IdPrefix != null)
        {
            _serviceIdRulePrefix = configuredNaming.IdPrefix;
        }

        if (!string.IsNullOrWhiteSpace(configuredNaming.NameRuleMode))
        {
            _serviceNameRuleMode = configuredNaming.NameRuleMode!;
        }

        if (configuredNaming.NamePrefix != null)
        {
            _serviceNameRulePrefix = configuredNaming.NamePrefix;
        }

        if (!string.IsNullOrWhiteSpace(configuredNaming.DescriptionRuleMode))
        {
            _serviceDescriptionRuleMode = configuredNaming.DescriptionRuleMode!;
        }

        if (configuredNaming.DescriptionPrefix != null)
        {
            _serviceDescriptionRulePrefix = configuredNaming.DescriptionPrefix;
        }
    }

    /// <summary>
    /// 数据根目录，默认 %ProgramData%\WSM。
    /// </summary>
    public string DataRoot => _dataRoot;

    public string WinSwStoreDirectory => _winSwStoreDirectory;

    public string ServicesDirectory => _servicesDirectory;

    public string DatabaseDirectory => _databaseDirectory;

    public string DatabasePath => _databasePath;

    public string AppLogsDirectory => Path.Combine(_dataRoot, "logs");

    public string OperationLogPath => _operationLogPath;

    public string ServiceIdRuleMode => _serviceIdRuleMode;

    public string ServiceIdRulePrefix => _serviceIdRulePrefix;

    public string ServiceNameRuleMode => _serviceNameRuleMode;

    public string ServiceNameRulePrefix => _serviceNameRulePrefix;

    public string ServiceDescriptionRuleMode => _serviceDescriptionRuleMode;

    public string ServiceDescriptionRulePrefix => _serviceDescriptionRulePrefix;

    /// <summary>
    /// 应用目录内的 WinSW 源文件（构建时复制到输出目录）。
    /// </summary>
    public string GetBundledWinSwPath(bool preferX64 = true)
    {
        var winswDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winsw");
        var preferredPath = Path.Combine(winswDirectory, preferX64 ? "WinSW-x64.exe" : "WinSW-x86.exe");
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var net461Path = Path.Combine(winswDirectory, "WinSW-net461.exe");
        if (File.Exists(net461Path))
        {
            return net461Path;
        }

        var fallbackPath = Path.Combine(winswDirectory, preferX64 ? "WinSW-x86.exe" : "WinSW-x64.exe");
        return fallbackPath;
    }

    /// <summary>
    /// 解析当前生效的 WinSW 可执行路径（内置，优先 x64）。
    /// </summary>
    public string ResolveWinSwExecutablePath() => GetBundledWinSwPath(preferX64: true);

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
        return Path.Combine(GetServiceDirectory(serviceId), WsmConstants.ServiceLogsSubdirectoryName);
    }

    /// <summary>
    /// 确保数据根目录及子目录存在。
    /// </summary>
    public void EnsureLayout()
    {
        Directory.CreateDirectory(WinSwStoreDirectory);
        Directory.CreateDirectory(ServicesDirectory);
        Directory.CreateDirectory(DatabaseDirectory);
        Directory.CreateDirectory(AppLogsDirectory);
        var operationLogDirectory = Path.GetDirectoryName(OperationLogPath);
        if (!string.IsNullOrWhiteSpace(operationLogDirectory))
        {
            Directory.CreateDirectory(operationLogDirectory);
        }
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

    /// <summary>
    /// 设置并持久化服务默认命名规则。
    /// </summary>
    public bool SetServiceNamingRules(
        string idRuleMode,
        string? idPrefix,
        string nameRuleMode,
        string? namePrefix,
        string descriptionRuleMode,
        string? descriptionPrefix)
    {
        if (string.IsNullOrWhiteSpace(idRuleMode))
        {
            throw new ArgumentException("服务 ID 默认规则不能为空。", nameof(idRuleMode));
        }

        if (string.IsNullOrWhiteSpace(nameRuleMode))
        {
            throw new ArgumentException("服务名称默认规则不能为空。", nameof(nameRuleMode));
        }

        if (string.IsNullOrWhiteSpace(descriptionRuleMode))
        {
            throw new ArgumentException("服务描述默认规则不能为空。", nameof(descriptionRuleMode));
        }

        var normalizedIdRule = idRuleMode.Trim();
        var normalizedIdPrefix = (idPrefix ?? string.Empty).Trim();
        var normalizedNameRule = nameRuleMode.Trim();
        var normalizedNamePrefix = (namePrefix ?? string.Empty).Trim();
        var normalizedDescriptionRule = descriptionRuleMode.Trim();
        var normalizedDescriptionPrefix = (descriptionPrefix ?? string.Empty).Trim();

        lock (_sync)
        {
            var unchanged = string.Equals(_serviceIdRuleMode, normalizedIdRule, StringComparison.Ordinal)
                            && string.Equals(_serviceIdRulePrefix, normalizedIdPrefix, StringComparison.Ordinal)
                            && string.Equals(_serviceNameRuleMode, normalizedNameRule, StringComparison.Ordinal)
                            && string.Equals(_serviceNameRulePrefix, normalizedNamePrefix, StringComparison.Ordinal)
                            && string.Equals(_serviceDescriptionRuleMode, normalizedDescriptionRule, StringComparison.Ordinal)
                            && string.Equals(_serviceDescriptionRulePrefix, normalizedDescriptionPrefix, StringComparison.Ordinal);
            if (unchanged)
            {
                return false;
            }

            _serviceIdRuleMode = normalizedIdRule;
            _serviceIdRulePrefix = normalizedIdPrefix;
            _serviceNameRuleMode = normalizedNameRule;
            _serviceNameRulePrefix = normalizedNamePrefix;
            _serviceDescriptionRuleMode = normalizedDescriptionRule;
            _serviceDescriptionRulePrefix = normalizedDescriptionPrefix;
            SaveConfiguredServiceNamingRules(
                _serviceIdRuleMode,
                _serviceIdRulePrefix,
                _serviceNameRuleMode,
                _serviceNameRulePrefix,
                _serviceDescriptionRuleMode,
                _serviceDescriptionRulePrefix);
        }

        return true;
    }

    private void UpdateDerivedPaths(string dataRoot)
    {
        _dataRoot = dataRoot;
        _winSwStoreDirectory = Path.Combine(_dataRoot, "winsw");
        _servicesDirectory = Path.Combine(_dataRoot, "services");
        _databaseDirectory = Path.Combine(_dataRoot, "data");
        _databasePath = Path.Combine(_databaseDirectory, "wsm.db");
        _operationLogPath = Path.Combine(_dataRoot, "logs", "operations.log");
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

    private static string GetServiceNamingRulesConfigFilePath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WSM");
        Directory.CreateDirectory(appData);
        return Path.Combine(appData, "service-naming-rules.txt");
    }

    private static (string? IdRuleMode, string? IdPrefix, string? NameRuleMode, string? NamePrefix, string? DescriptionRuleMode, string? DescriptionPrefix) LoadConfiguredServiceNamingRules()
    {
        var configPath = GetServiceNamingRulesConfigFilePath();
        if (!File.Exists(configPath))
        {
            return (null, null, null, null, null, null);
        }

        var lines = File.ReadAllLines(configPath);
        return (
            lines.Length > 0 ? lines[0].Trim() : null,
            lines.Length > 1 ? lines[1] : null,
            lines.Length > 2 ? lines[2].Trim() : null,
            lines.Length > 3 ? lines[3] : null,
            lines.Length > 4 ? lines[4].Trim() : null,
            lines.Length > 5 ? lines[5] : null);
    }

    private static void SaveConfiguredServiceNamingRules(
        string idRuleMode,
        string idPrefix,
        string nameRuleMode,
        string namePrefix,
        string descriptionRuleMode,
        string descriptionPrefix)
    {
        var configPath = GetServiceNamingRulesConfigFilePath();
        File.WriteAllLines(configPath, new[]
        {
            idRuleMode ?? string.Empty,
            idPrefix ?? string.Empty,
            nameRuleMode ?? string.Empty,
            namePrefix ?? string.Empty,
            descriptionRuleMode ?? string.Empty,
            descriptionPrefix ?? string.Empty
        });
    }
}
