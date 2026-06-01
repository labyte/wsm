using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using WSM.App.Shared.Navigation;
using WSM.App.Shared.Services;
using WSM.Core;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Infrastructure.Paths;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 设置页 ViewModel。
/// </summary>
public partial class SettingsViewModel : ObservableObject, INavigationAware
{
    public sealed class DefaultRuleOption
    {
        public DefaultRuleOption(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }
        public string Display { get; }
    }

    public sealed class WinSwToolOption
    {
        public WinSwToolOption(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }
        public string Display { get; }
    }

    private readonly WsmPaths _paths;
    private readonly AdminElevationService _adminElevation;
    private readonly ISnackbarService _snackbarService;
    private readonly IServiceRepository _serviceRepository;
    private readonly IWinSwHostService _winSwHostService;
    private readonly INavigationService _navigationService;
    private readonly ITrayIconService _trayIconService;
    private readonly ICloseWindowPreferenceStore _closeWindowPreferenceStore;
    private bool _isLoadingCloseWindowBehavior;

    public SettingsViewModel(
        WsmPaths paths,
        AdminElevationService adminElevation,
        ISnackbarService snackbarService,
        IServiceRepository serviceRepository,
        IWinSwHostService winSwHostService,
        INavigationService navigationService,
        ITrayIconService trayIconService,
        ICloseWindowPreferenceStore closeWindowPreferenceStore)
    {
        _paths = paths;
        _adminElevation = adminElevation;
        _snackbarService = snackbarService;
        _serviceRepository = serviceRepository;
        _winSwHostService = winSwHostService;
        _navigationService = navigationService;
        _trayIconService = trayIconService;
        _closeWindowPreferenceStore = closeWindowPreferenceStore;
        WinSwToolModeOptions = new List<WinSwToolOption>
        {
            new(WsmPaths.WinSwToolModeBundledX64, "内置 WinSW x64（推荐）"),
            new(WsmPaths.WinSwToolModeBundledX86, "内置 WinSW x86"),
            new(WsmPaths.WinSwToolModeBundledNet461, "内置 WinSW .NET Framework"),
            new(WsmPaths.WinSwToolModeGlobal, "全局 winsw 命令"),
            new(WsmPaths.WinSwToolModeCustom, "自定义可执行路径")
        };
        DefaultRuleOptions = new List<DefaultRuleOption>
        {
            new(WsmPaths.DefaultRuleProgramName, "程序名称"),
            new(WsmPaths.DefaultRulePrefixProgramName, "前缀 + 程序名称")
        };
        AppVersion = ResolveAppVersion();
        DataRootPath = _paths.DataRoot;
        WinSwToolMode = _paths.WinSwToolMode;
        CustomWinSwPath = _paths.CustomWinSwPath;
        OperationLogPath = _paths.OperationLogPath;
        ServiceIdRuleMode = _paths.ServiceIdRuleMode;
        ServiceIdRulePrefix = _paths.ServiceIdRulePrefix;
        ServiceNameRuleMode = _paths.ServiceNameRuleMode;
        ServiceNameRulePrefix = _paths.ServiceNameRulePrefix;
        ServiceDescriptionRuleMode = _paths.ServiceDescriptionRuleMode;
        ServiceDescriptionRulePrefix = _paths.ServiceDescriptionRulePrefix;
        UpdateWinSwToolDerivedState();
        RefreshAdminStatus();
        LoadCloseWindowBehavior();
    }

    public string AppVersion { get; }

    private static string ResolveAppVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            var infoVersion = entryAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                return infoVersion!.Trim();
            }

            var assemblyVersion = entryAssembly.GetName().Version?.ToString();
            if (!string.IsNullOrWhiteSpace(assemblyVersion))
            {
                return assemblyVersion!;
            }
        }

        var sharedAssemblyVersion = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(sharedAssemblyVersion) ? "0.1.0" : sharedAssemblyVersion!;
    }

    public IReadOnlyList<WinSwToolOption> WinSwToolModeOptions { get; }
    public IReadOnlyList<DefaultRuleOption> DefaultRuleOptions { get; }

    [ObservableProperty]
    private string _hint = string.Empty;

    [ObservableProperty]
    private bool _minimizeOnClose = true;

    [ObservableProperty]
    private string _adminStatusText = string.Empty;

    [ObservableProperty]
    private bool _isRunningAsAdministrator;

    [ObservableProperty]
    private bool _canRestartElevated;

    [ObservableProperty]
    private string _dataRootPath = string.Empty;

    [ObservableProperty]
    private bool _isApplyingDataRoot;

    [ObservableProperty]
    private string _winSwToolMode = WsmPaths.WinSwToolModeBundledX64;

    [ObservableProperty]
    private string _customWinSwPath = string.Empty;

    [ObservableProperty]
    private bool _isCustomWinSwPathEnabled;

    [ObservableProperty]
    private string _effectiveWinSwPath = string.Empty;

    [ObservableProperty]
    private bool _isApplyingWinSwTool;

    [ObservableProperty]
    private string _operationLogPath = string.Empty;

    [ObservableProperty]
    private bool _isApplyingOperationLogPath;

    [ObservableProperty]
    private string _serviceIdRuleMode = WsmPaths.DefaultRuleProgramName;

    [ObservableProperty]
    private string _serviceIdRulePrefix = "svc-";

    [ObservableProperty]
    private string _serviceNameRuleMode = WsmPaths.DefaultRuleProgramName;

    [ObservableProperty]
    private string _serviceNameRulePrefix = string.Empty;

    [ObservableProperty]
    private string _serviceDescriptionRuleMode = WsmPaths.DefaultRulePrefixProgramName;

    [ObservableProperty]
    private string _serviceDescriptionRulePrefix = "由 WSM 托管：";

    [ObservableProperty]
    private bool _isApplyingServiceNamingRules;

    public bool IsServiceIdPrefixVisible => string.Equals(ServiceIdRuleMode, WsmPaths.DefaultRulePrefixProgramName, StringComparison.OrdinalIgnoreCase);
    public bool IsServiceNamePrefixVisible => string.Equals(ServiceNameRuleMode, WsmPaths.DefaultRulePrefixProgramName, StringComparison.OrdinalIgnoreCase);
    public bool IsServiceDescriptionPrefixVisible => string.Equals(ServiceDescriptionRuleMode, WsmPaths.DefaultRulePrefixProgramName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 与 <see cref="MinimizeOnClose"/> 互斥，用于“退出程序”单选按钮绑定。
    /// </summary>
    public bool ExitOnClose
    {
        get => !MinimizeOnClose;
        set => MinimizeOnClose = !value;
    }

    public void OnNavigatedTo()
    {
        LoadCloseWindowBehavior();
        DataRootPath = _paths.DataRoot;
        WinSwToolMode = _paths.WinSwToolMode;
        CustomWinSwPath = _paths.CustomWinSwPath;
        OperationLogPath = _paths.OperationLogPath;
        ServiceIdRuleMode = _paths.ServiceIdRuleMode;
        ServiceIdRulePrefix = _paths.ServiceIdRulePrefix;
        ServiceNameRuleMode = _paths.ServiceNameRuleMode;
        ServiceNameRulePrefix = _paths.ServiceNameRulePrefix;
        ServiceDescriptionRuleMode = _paths.ServiceDescriptionRuleMode;
        ServiceDescriptionRulePrefix = _paths.ServiceDescriptionRulePrefix;
        UpdateWinSwToolDerivedState();
        RefreshAdminStatus();
    }

    [RelayCommand]
    private void RestartAsAdministrator()
    {
        if (_adminElevation.TryRestartAsAdministrator())
        {
            return;
        }

        _snackbarService.ShowError(
            $"无法自动提权。请以管理员身份运行：{WsmConstants.AppDisplayName}.exe");
    }

    private void RefreshAdminStatus()
    {
        IsRunningAsAdministrator = _adminElevation.IsRunningAsAdministrator;
        CanRestartElevated = _adminElevation.CanRestartElevated;
        AdminStatusText = _adminElevation.GetAdminStatusText();
    }

    private void LoadCloseWindowBehavior()
    {
        _isLoadingCloseWindowBehavior = true;
        try
        {
            MinimizeOnClose = _closeWindowPreferenceStore.LoadMinimizeOnClose()
                ?? _trayIconService.MinimizeOnClose;
            _trayIconService.MinimizeOnClose = MinimizeOnClose;
            UpdateCloseWindowHint();
        }
        finally
        {
            _isLoadingCloseWindowBehavior = false;
        }
    }

    private void ApplyCloseWindowBehavior(bool minimizeOnClose)
    {
        _trayIconService.MinimizeOnClose = minimizeOnClose;
        _closeWindowPreferenceStore.SaveMinimizeOnClose(minimizeOnClose);
        UpdateCloseWindowHint();
        _snackbarService.ShowInfo(minimizeOnClose
            ? "已设置：关闭主窗口时最小化到托盘。"
            : "已设置：关闭主窗口时退出程序。");
    }

    private void UpdateCloseWindowHint()
    {
        Hint = MinimizeOnClose
            ? "关闭主窗口时将最小化到系统托盘，程序继续在后台运行。"
            : "关闭主窗口时将退出程序。仍可通过托盘菜单中的「退出」结束运行。";
    }

    [RelayCommand]
    private void BrowseDataRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择 WSM 数据目录",
            SelectedPath = DataRootPath
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            DataRootPath = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void BrowseWinSwPath()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            FileName = CustomWinSwPath
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            CustomWinSwPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseOperationLogPath()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(OperationLogPath) ? "operations.log" : Path.GetFileName(OperationLogPath),
            InitialDirectory = ResolveInitialDirectory(OperationLogPath, _paths.DataRoot)
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            OperationLogPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task ApplyDataRootAsync()
    {
        if (IsApplyingDataRoot)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(DataRootPath))
        {
            _snackbarService.ShowWarning("数据目录不能为空。");
            return;
        }

        IsApplyingDataRoot = true;
        try
        {
            var currentRoot = Path.GetFullPath(_paths.DataRoot);
            var targetRoot = Path.GetFullPath(DataRootPath.Trim());
            if (string.Equals(currentRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
            {
                _snackbarService.ShowInfo("数据目录未变化。");
                return;
            }

            if (IsNestedPath(currentRoot, targetRoot) || IsNestedPath(targetRoot, currentRoot))
            {
                _snackbarService.ShowError("新旧数据目录不能互为父子目录，请选择独立路径。");
                return;
            }

            var services = await _serviceRepository.GetAllAsync().ConfigureAwait(true);
            var runningServiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var service in services)
            {
                var status = await _winSwHostService.GetStatusAsync(service.Id).ConfigureAwait(true);
                if (status != ServiceRuntimeStatus.Running && status != ServiceRuntimeStatus.StartPending)
                {
                    continue;
                }

                runningServiceIds.Add(service.Id);
                var stopResult = await _winSwHostService.StopAsync(service.Id).ConfigureAwait(true);
                if (!stopResult.Success)
                {
                    _snackbarService.ShowError($"停止服务「{service.DisplayName}」失败：{stopResult.Message}");
                    return;
                }
            }

            MigrateDataRoot(currentRoot, targetRoot);

            var changed = _paths.SetDataRoot(targetRoot);
            DataRootPath = _paths.DataRoot;
            if (!changed)
            {
                _snackbarService.ShowInfo("数据目录未变化。");
                return;
            }

            foreach (var service in services)
            {
                var rebound = await RebindServiceToNewDataRootAsync(service).ConfigureAwait(true);
                if (!rebound.Success)
                {
                    _snackbarService.ShowError($"迁移服务「{service.DisplayName}」失败：{rebound.Message}");
                    return;
                }
            }

            foreach (var serviceId in runningServiceIds)
            {
                var startResult = await _winSwHostService.StartAsync(serviceId).ConfigureAwait(true);
                if (!startResult.Success)
                {
                    _snackbarService.ShowWarning($"服务「{serviceId}」迁移后未能自动恢复运行：{startResult.Message}");
                }
            }

            _navigationService.NavigateTo(AppPage.ServiceList);
            _snackbarService.ShowSuccess("数据目录已迁移并重绑定服务，原运行中的服务已尝试恢复。");
        }
        catch (System.Exception ex)
        {
            _snackbarService.ShowError("更新数据目录失败：" + ex.Message);
        }
        finally
        {
            IsApplyingDataRoot = false;
        }
    }

    [RelayCommand]
    private Task ApplyWinSwToolAsync()
    {
        if (IsApplyingWinSwTool)
        {
            return Task.CompletedTask;
        }

        IsApplyingWinSwTool = true;
        try
        {
            if (WinSwToolMode == WsmPaths.WinSwToolModeCustom)
            {
                if (string.IsNullOrWhiteSpace(CustomWinSwPath))
                {
                    _snackbarService.ShowWarning("请先指定自定义 WinSW 可执行文件路径。");
                    return Task.CompletedTask;
                }

                if (!File.Exists(CustomWinSwPath))
                {
                    _snackbarService.ShowError("指定的 WinSW 可执行文件不存在。");
                    return Task.CompletedTask;
                }
            }

            var changed = _paths.SetWinSwTool(WinSwToolMode, CustomWinSwPath);
            UpdateWinSwToolDerivedState();
            _snackbarService.ShowInfo(changed
                ? "WinSW 执行配置已应用。"
                : "WinSW 执行配置未变化。");
        }
        finally
        {
            IsApplyingWinSwTool = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ApplyOperationLogPathAsync()
    {
        if (IsApplyingOperationLogPath)
        {
            return Task.CompletedTask;
        }

        IsApplyingOperationLogPath = true;
        try
        {
            var targetPath = string.IsNullOrWhiteSpace(OperationLogPath)
                ? null
                : OperationLogPath.Trim();

            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = Path.GetFullPath(targetPath);
                var directory = Path.GetDirectoryName(targetPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    _snackbarService.ShowError("操作日志文件路径无效。");
                    return Task.CompletedTask;
                }

                Directory.CreateDirectory(directory);
            }

            var changed = _paths.SetOperationLogPath(targetPath);
            OperationLogPath = _paths.OperationLogPath;
            _snackbarService.ShowInfo(changed
                ? "操作日志文件路径已应用。"
                : "操作日志文件路径未变化。");
        }
        catch (Exception ex)
        {
            _snackbarService.ShowError("应用操作日志文件路径失败：" + ex.Message);
        }
        finally
        {
            IsApplyingOperationLogPath = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ApplyServiceNamingRulesAsync()
    {
        if (IsApplyingServiceNamingRules)
        {
            return Task.CompletedTask;
        }

        IsApplyingServiceNamingRules = true;
        try
        {
            var changed = _paths.SetServiceNamingRules(
                ServiceIdRuleMode,
                ServiceIdRulePrefix,
                ServiceNameRuleMode,
                ServiceNameRulePrefix,
                ServiceDescriptionRuleMode,
                ServiceDescriptionRulePrefix);
            _snackbarService.ShowInfo(changed
                ? "服务默认命名规则已应用。"
                : "服务默认命名规则未变化。");
        }
        catch (Exception ex)
        {
            _snackbarService.ShowError("应用服务默认命名规则失败：" + ex.Message);
        }
        finally
        {
            IsApplyingServiceNamingRules = false;
        }

        return Task.CompletedTask;
    }

    private static void MigrateDataRoot(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            Directory.CreateDirectory(targetRoot);
            return;
        }

        Directory.CreateDirectory(targetRoot);
        CopyDirectoryRecursive(sourceRoot, targetRoot);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(directory));
            CopyDirectoryRecursive(directory, targetSubDir);
        }
    }

    private static bool IsNestedPath(string parentPath, string candidateChildPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(candidateChildPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveInitialDirectory(string? path, string fallbackDirectory)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var normalized = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(normalized);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return Directory.Exists(fallbackDirectory)
            ? fallbackDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    partial void OnMinimizeOnCloseChanged(bool value)
    {
        OnPropertyChanged(nameof(ExitOnClose));
        if (_isLoadingCloseWindowBehavior)
        {
            return;
        }

        ApplyCloseWindowBehavior(value);
    }

    partial void OnWinSwToolModeChanged(string value)
    {
        UpdateWinSwToolDerivedState();
    }

    partial void OnCustomWinSwPathChanged(string value)
    {
        UpdateWinSwToolDerivedState();
    }

    partial void OnServiceIdRuleModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsServiceIdPrefixVisible));
    }

    partial void OnServiceNameRuleModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsServiceNamePrefixVisible));
    }

    partial void OnServiceDescriptionRuleModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsServiceDescriptionPrefixVisible));
    }

    private void UpdateWinSwToolDerivedState()
    {
        IsCustomWinSwPathEnabled = string.Equals(WinSwToolMode, WsmPaths.WinSwToolModeCustom, StringComparison.Ordinal);
        EffectiveWinSwPath = ResolveEffectiveWinSwPathPreview();
    }

    private string ResolveEffectiveWinSwPathPreview()
    {
        if (string.Equals(WinSwToolMode, WsmPaths.WinSwToolModeCustom, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(CustomWinSwPath))
        {
            return CustomWinSwPath;
        }

        if (string.Equals(WinSwToolMode, WsmPaths.WinSwToolModeGlobal, StringComparison.Ordinal))
        {
            return "winsw（PATH）";
        }

        return _paths.ResolveWinSwExecutablePath();
    }

    private async Task<OperationResult> RebindServiceToNewDataRootAsync(ManagedService service)
    {
        // 优先 refresh：可更新 SCM 中的包装器路径
        var refreshResult = await _winSwHostService.RefreshAsync(service).ConfigureAwait(true);
        if (refreshResult.Success)
        {
            return refreshResult;
        }

        // refresh 失败时，执行卸载+重装兜底
        var uninstallResult = await _winSwHostService.UninstallAsync(service.Id).ConfigureAwait(true);
        if (!uninstallResult.Success)
        {
            return OperationResult.Fail("卸载旧服务失败：" + uninstallResult.Message, uninstallResult.Exception, uninstallResult.ErrorCode);
        }

        var installModel = CloneForReinstall(service);
        installModel.StartAfterInstall = false;
        var installResult = await _winSwHostService.InstallAsync(installModel).ConfigureAwait(true);
        return installResult.Success
            ? OperationResult.Ok()
            : OperationResult.Fail("重装服务失败：" + installResult.Message, installResult.Exception, installResult.ErrorCode);
    }

    private static ManagedService CloneForReinstall(ManagedService source)
    {
        return new ManagedService
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Description = source.Description,
            ExecutablePath = source.ExecutablePath,
            WorkingDirectory = source.WorkingDirectory,
            Arguments = source.Arguments,
            EnvironmentVariables = source.EnvironmentVariables
                .Select(x => new EnvVariable { Name = x.Name, Value = x.Value })
                .ToList(),
            StartMode = source.StartMode,
            DelayedAutoStart = source.DelayedAutoStart,
            AutoRefresh = source.AutoRefresh,
            HideWindow = source.HideWindow,
            Dependencies = source.Dependencies.ToList(),
            StopTimeoutSeconds = source.StopTimeoutSeconds,
            StartAfterInstall = source.StartAfterInstall,
            FailurePolicy = new FailurePolicy
            {
                ResetFailurePeriod = source.FailurePolicy.ResetFailurePeriod,
                Actions = source.FailurePolicy.Actions
                    .Select(x => new FailureActionEntry { Action = x.Action, Delay = x.Delay })
                    .ToList()
            },
            RecoverySettings = new ServiceRecoverySettings
            {
                EnableCrashRecovery = source.RecoverySettings.EnableCrashRecovery,
                CrashRestartDelaySeconds = source.RecoverySettings.CrashRestartDelaySeconds,
                CrashMaxRestartCount = source.RecoverySettings.CrashMaxRestartCount,
                EnableHangRecovery = source.RecoverySettings.EnableHangRecovery,
                HangDetectionTimeoutSeconds = source.RecoverySettings.HangDetectionTimeoutSeconds,
                EnablePseudoHangRecovery = source.RecoverySettings.EnablePseudoHangRecovery,
                PseudoHangTimeoutSeconds = source.RecoverySettings.PseudoHangTimeoutSeconds,
                RestartOnAnomaly = source.RecoverySettings.RestartOnAnomaly
            },
            LogPolicy = new LogPolicy
            {
                Mode = source.LogPolicy.Mode,
                SizeThresholdKb = source.LogPolicy.SizeThresholdKb,
                KeepFiles = source.LogPolicy.KeepFiles
            },
            LogSourceMode = source.LogSourceMode,
            ExternalLogFilePath = source.ExternalLogFilePath,
            ExternalLogDirectoryPath = source.ExternalLogDirectoryPath,
            ExternalLogFileExtensions = source.ExternalLogFileExtensions,
            ExternalLogRealtimeTracking = source.ExternalLogRealtimeTracking,
            ExternalLogTailLines = source.ExternalLogTailLines
        };
    }
}
