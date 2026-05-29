using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WSM.App.Shared.Navigation;
using WSM.App.Shared.Services;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Core.Services;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 添加服务 ViewModel（单页分区表单）。
/// </summary>
public partial class ServiceInstallViewModel : ObservableObject, INavigationAware
{
    private readonly IWinSwHostService _winSwHostService;
    private readonly IServiceRepository _serviceRepository;
    private readonly ISnackbarService _snackbarService;
    private readonly INavigationService _navigationService;
    private readonly AdminElevationService _adminElevation;
    private readonly ServiceConfigValidator _validator = new ServiceConfigValidator();
    private readonly ServiceIdSuggester _idSuggester = new ServiceIdSuggester();

    public ServiceInstallViewModel(
        IWinSwHostService winSwHostService,
        IServiceRepository serviceRepository,
        ISnackbarService snackbarService,
        INavigationService navigationService,
        AdminElevationService adminElevation)
    {
        _winSwHostService = winSwHostService;
        _serviceRepository = serviceRepository;
        _snackbarService = snackbarService;
        _navigationService = navigationService;
        _adminElevation = adminElevation;
    }

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _serviceId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private ManagedServiceStartMode _startMode = ManagedServiceStartMode.Automatic;

    [ObservableProperty]
    private bool _delayedAutoStart = true;

    [ObservableProperty]
    private int _stopTimeoutSeconds = 15;

    [ObservableProperty]
    private bool _startAfterInstall = true;

    [ObservableProperty]
    private bool _enableCrashRecovery = true;

    [ObservableProperty]
    private int _crashRestartDelaySeconds = 5;

    [ObservableProperty]
    private int _crashMaxRestartCount = 3;

    [ObservableProperty]
    private bool _enableHangRecovery;

    [ObservableProperty]
    private int _hangDetectionTimeoutSeconds = 120;

    [ObservableProperty]
    private bool _enablePseudoHangRecovery;

    [ObservableProperty]
    private int _pseudoHangTimeoutSeconds = 300;

    [ObservableProperty]
    private bool _restartOnAnomaly = true;

    [ObservableProperty]
    private bool _isInstalling;

    public Array StartModeOptions => Enum.GetValues(typeof(ManagedServiceStartMode));

    [ObservableProperty]
    private string _executablePathError = string.Empty;

    [ObservableProperty]
    private string _serviceIdError = string.Empty;

    [ObservableProperty]
    private string _displayNameError = string.Empty;

    [ObservableProperty]
    private string _stopTimeoutError = string.Empty;

    [ObservableProperty]
    private string _hangRecoveryError = string.Empty;

    partial void OnExecutablePathChanged(string value) => ValidateAllFields(showSnackbar: false);
    partial void OnServiceIdChanged(string value) => ValidateAllFields(showSnackbar: false);
    partial void OnDisplayNameChanged(string value) => ValidateAllFields(showSnackbar: false);
    partial void OnStopTimeoutSecondsChanged(int value) => ValidateAllFields(showSnackbar: false);
    partial void OnEnableHangRecoveryChanged(bool value) => ValidateAllFields(showSnackbar: false);
    partial void OnEnablePseudoHangRecoveryChanged(bool value) => ValidateAllFields(showSnackbar: false);
    partial void OnHangDetectionTimeoutSecondsChanged(int value) => ValidateAllFields(showSnackbar: false);
    partial void OnPseudoHangTimeoutSecondsChanged(int value) => ValidateAllFields(showSnackbar: false);

    public void OnNavigatedTo()
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            ResetForm();
        }
    }

    [RelayCommand]
    private void BrowseExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            Title = "选择要注册为服务的程序"
        };

        if (dialog.ShowDialog() == true)
        {
            ExecutablePath = dialog.FileName;
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            Arguments = string.Empty;
            ServiceId = _idSuggester.SuggestFromExecutablePath(dialog.FileName);
            DisplayName = Path.GetFileNameWithoutExtension(dialog.FileName);
            Description = $"由 WSM 托管：{Path.GetFileName(dialog.FileName)}";
            ValidateAllFields(showSnackbar: false);
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (!ValidateAllFields(showSnackbar: true))
        {
            return;
        }

        var service = BuildManagedService();
        var existingIds = (await _serviceRepository.GetAllAsync().ConfigureAwait(true))
            .Select(x => x.Id)
            .ToList();

        var validation = _validator.Validate(service, existingIds);
        if (!validation.IsValid)
        {
            _snackbarService.ShowError(validation.Errors[0].Message);
            return;
        }

        IsInstalling = true;
        _navigationService.NavigateTo(AppPage.Logs);

        if (!_adminElevation.IsRunningAsAdministrator)
        {
            _snackbarService.ShowWarning("需要管理员权限，正在请求 UAC 提权...");
            if (_adminElevation.TryRestartAsAdministrator())
            {
                IsInstalling = false;
                return;
            }

            _snackbarService.ShowError("无法提权。请直接运行 WSM.exe（会自动弹出 UAC），或在设置中点击「以管理员身份重启」。");
            IsInstalling = false;
            return;
        }

        try
        {
            var result = await _winSwHostService.InstallAsync(service).ConfigureAwait(true);
            if (result.Success)
            {
                _snackbarService.ShowSuccess(result.Message);
                ResetForm();
                _navigationService.NavigateTo(AppPage.ServiceList);
            }
            else
            {
                _snackbarService.ShowError(result.Message);
                if (result.ErrorCode == "ADMIN_REQUIRED" && _adminElevation.TryRestartAsAdministrator())
                {
                    return;
                }
            }
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private bool ValidateAllFields(bool showSnackbar)
    {
        ExecutablePathError = string.Empty;
        ServiceIdError = string.Empty;
        DisplayNameError = string.Empty;
        StopTimeoutError = string.Empty;
        HangRecoveryError = string.Empty;

        if (string.IsNullOrWhiteSpace(ExecutablePath) || !File.Exists(ExecutablePath))
        {
            ExecutablePathError = "请选择有效的可执行文件。";
        }

        if (string.IsNullOrWhiteSpace(ServiceId) || !_validator.IsValidIdFormat(ServiceId))
        {
            ServiceIdError = "服务 ID 必须以小写字母开头，且仅包含小写字母、数字和连字符。";
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayNameError = "显示名称不能为空。";
        }

        if (StopTimeoutSeconds <= 0)
        {
            StopTimeoutError = "停止超时必须大于 0 秒。";
        }

        if (EnableHangRecovery && HangDetectionTimeoutSeconds <= 0)
        {
            HangRecoveryError = "卡死判定超时必须大于 0 秒。";
        }

        if (EnablePseudoHangRecovery && PseudoHangTimeoutSeconds <= 0)
        {
            HangRecoveryError = "假死判定超时必须大于 0 秒。";
        }

        var isValid = string.IsNullOrWhiteSpace(ExecutablePathError)
            && string.IsNullOrWhiteSpace(ServiceIdError)
            && string.IsNullOrWhiteSpace(DisplayNameError)
            && string.IsNullOrWhiteSpace(StopTimeoutError)
            && string.IsNullOrWhiteSpace(HangRecoveryError);

        if (!isValid && showSnackbar)
        {
            var firstError = new[]
            {
                ExecutablePathError,
                ServiceIdError,
                DisplayNameError,
                StopTimeoutError,
                HangRecoveryError
            }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (!string.IsNullOrWhiteSpace(firstError))
            {
                _snackbarService.ShowWarning(firstError);
            }
        }

        return isValid;
    }

    private ManagedService BuildManagedService()
    {
        return new ManagedService
        {
            Id = ServiceId.Trim(),
            DisplayName = DisplayName.Trim(),
            Description = Description?.Trim() ?? string.Empty,
            ExecutablePath = ExecutablePath.Trim(),
            WorkingDirectory = WorkingDirectory.Trim(),
            Arguments = Arguments?.Trim() ?? string.Empty,
            StartMode = StartMode,
            DelayedAutoStart = DelayedAutoStart,
            StopTimeoutSeconds = StopTimeoutSeconds,
            StartAfterInstall = StartAfterInstall,
            FailurePolicy = BuildFailurePolicy(),
            RecoverySettings = new ServiceRecoverySettings
            {
                EnableCrashRecovery = EnableCrashRecovery,
                CrashRestartDelaySeconds = CrashRestartDelaySeconds,
                CrashMaxRestartCount = CrashMaxRestartCount,
                EnableHangRecovery = EnableHangRecovery,
                HangDetectionTimeoutSeconds = HangDetectionTimeoutSeconds,
                EnablePseudoHangRecovery = EnablePseudoHangRecovery,
                PseudoHangTimeoutSeconds = PseudoHangTimeoutSeconds,
                RestartOnAnomaly = RestartOnAnomaly
            },
            LogPolicy = LogPolicy.CreateDefault()
        };
    }

    private FailurePolicy BuildFailurePolicy()
    {
        if (!EnableCrashRecovery)
        {
            return FailurePolicy.CreateFromTemplate(FailurePolicyTemplate.MonitorOnly);
        }

        var actions = new System.Collections.Generic.List<FailureActionEntry>();
        for (var i = 0; i < Math.Max(1, CrashMaxRestartCount); i++)
        {
            actions.Add(new FailureActionEntry
            {
                Action = FailureActionType.Restart,
                Delay = $"{Math.Max(1, CrashRestartDelaySeconds)} sec"
            });
        }

        actions.Add(new FailureActionEntry { Action = FailureActionType.None, Delay = "0 sec" });
        return new FailurePolicy
        {
            ResetFailurePeriod = "1 hour",
            Actions = actions
        };
    }

    private void ResetForm()
    {
        ExecutablePath = string.Empty;
        WorkingDirectory = string.Empty;
        Arguments = string.Empty;
        ServiceId = string.Empty;
        DisplayName = string.Empty;
        Description = string.Empty;
        StartMode = ManagedServiceStartMode.Automatic;
        DelayedAutoStart = true;
        StopTimeoutSeconds = 15;
        StartAfterInstall = true;
        EnableCrashRecovery = true;
        CrashRestartDelaySeconds = 5;
        CrashMaxRestartCount = 3;
        EnableHangRecovery = false;
        HangDetectionTimeoutSeconds = 120;
        EnablePseudoHangRecovery = false;
        PseudoHangTimeoutSeconds = 300;
        RestartOnAnomaly = true;
        ExecutablePathError = string.Empty;
        ServiceIdError = string.Empty;
        DisplayNameError = string.Empty;
        StopTimeoutError = string.Empty;
        HangRecoveryError = string.Empty;
    }
}
