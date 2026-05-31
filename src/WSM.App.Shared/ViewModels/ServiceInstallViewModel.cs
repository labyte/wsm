using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WSM.App.Shared.Navigation;
using WSM.App.Shared.Services;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Core.Services;
using WSM.Infrastructure.Paths;

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
    private readonly WsmPaths _paths;
    private readonly ServiceConfigValidator _validator = new ServiceConfigValidator();
    private readonly ServiceIdSuggester _idSuggester = new ServiceIdSuggester();

    public ServiceInstallViewModel(
        IWinSwHostService winSwHostService,
        IServiceRepository serviceRepository,
        ISnackbarService snackbarService,
        INavigationService navigationService,
        AdminElevationService adminElevation,
        WsmPaths paths)
    {
        _winSwHostService = winSwHostService;
        _serviceRepository = serviceRepository;
        _snackbarService = snackbarService;
        _navigationService = navigationService;
        _adminElevation = adminElevation;
        _paths = paths;
        Draft.PropertyChanged += DraftOnPropertyChanged;
        RefreshInstallPermissionState();
        ResetForm();
    }

    [ObservableProperty]
    private ServiceConfigDraft _draft = new();

    partial void OnDraftChanged(ServiceConfigDraft value)
    {
        if (value == null)
        {
            return;
        }

        value.PropertyChanged += DraftOnPropertyChanged;
        ValidateAllFields(showSnackbar: false);
    }

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _canInstallService;

    [ObservableProperty]
    private string _installServiceToolTip = string.Empty;

    [ObservableProperty]
    private bool _showInstallPermissionHint;

    [ObservableProperty]
    private string _installPermissionHintText = string.Empty;

    public System.Collections.Generic.IReadOnlyList<DisplayOption<ManagedServiceStartMode>> StartModeOptions =>
        ServiceConfigUiOptions.StartModeOptions;

    public System.Collections.Generic.IReadOnlyList<DisplayOption<ServiceLogSourceMode>> LogSourceOptions =>
        ServiceConfigUiOptions.LogSourceOptions;

    public System.Collections.Generic.IReadOnlyList<DisplayOption<LogMode>> WinSwLogModeOptions =>
        ServiceConfigUiOptions.WinSwLogModeOptions;

    public System.Collections.Generic.IReadOnlyList<DisplayOption<FailureActionType>> FailureActionOptions =>
        ServiceConfigUiOptions.FailureActionOptions;

    public System.Collections.Generic.IReadOnlyList<DisplayOption<string>> ResetFailureUnitOptions =>
        ServiceConfigUiOptions.ResetFailureUnitOptions;

    [ObservableProperty]
    private string _executablePathError = string.Empty;

    [ObservableProperty]
    private string _serviceIdError = string.Empty;

    [ObservableProperty]
    private string _displayNameError = string.Empty;

    [ObservableProperty]
    private string _stopTimeoutError = string.Empty;

    public void OnNavigatedTo()
    {
        RefreshInstallPermissionState();

        if (string.IsNullOrWhiteSpace(Draft.ExecutablePath))
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
            Draft.ExecutablePath = dialog.FileName;
            Draft.WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            Draft.Arguments = string.Empty;
            var programName = Path.GetFileNameWithoutExtension(dialog.FileName);
            Draft.Id = BuildDefaultServiceId(programName);
            Draft.DisplayName = BuildDefaultText(programName, _paths.ServiceNameRuleMode, _paths.ServiceNameRulePrefix);
            Draft.Description = BuildDefaultText(programName, _paths.ServiceDescriptionRuleMode, _paths.ServiceDescriptionRulePrefix);
            ValidateAllFields(showSnackbar: false);
        }
    }

    [RelayCommand]
    private void BrowseExternalLogDirectory()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "请选择日志目录",
            SelectedPath = Draft.ExternalLogDirectoryPath
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            Draft.ExternalLogDirectoryPath = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        RefreshInstallPermissionState();
        if (!CanInstallService)
        {
            _snackbarService.ShowError("安装服务需要管理员权限。请先在设置中点击“以管理员身份重启”，再重新执行安装。");
            return;
        }

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

        if (string.IsNullOrWhiteSpace(Draft.ExecutablePath) || !File.Exists(Draft.ExecutablePath))
        {
            ExecutablePathError = "请选择有效的可执行文件。";
        }

        if (string.IsNullOrWhiteSpace(Draft.Id) || !_validator.IsValidIdFormat(Draft.Id))
        {
            ServiceIdError = "服务 ID 必须以小写字母开头，且仅包含小写字母、数字和连字符。";
        }

        if (string.IsNullOrWhiteSpace(Draft.DisplayName))
        {
            DisplayNameError = "显示名称不能为空。";
        }

        if (Draft.StopTimeoutSeconds <= 0)
        {
            StopTimeoutError = "停止超时必须大于 0 秒。";
        }

        var isValid = string.IsNullOrWhiteSpace(ExecutablePathError)
            && string.IsNullOrWhiteSpace(ServiceIdError)
            && string.IsNullOrWhiteSpace(DisplayNameError)
            && string.IsNullOrWhiteSpace(StopTimeoutError);

        if (!isValid && showSnackbar)
        {
            var firstError = new[]
            {
                ExecutablePathError,
                ServiceIdError,
                DisplayNameError,
                StopTimeoutError
            }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (!string.IsNullOrWhiteSpace(firstError))
            {
                _snackbarService.ShowWarning(firstError);
            }
        }

        return isValid;
    }

    private void RefreshInstallPermissionState()
    {
        var isAdministrator = _adminElevation.IsRunningAsAdministrator;
        CanInstallService = isAdministrator;
        InstallServiceToolTip = isAdministrator
            ? "安装服务"
            : "需要管理员权限。请先在设置中点击“以管理员身份重启”。";
        ShowInstallPermissionHint = !isAdministrator;
        InstallPermissionHintText = isAdministrator
            ? string.Empty
            : "当前为非管理员模式，安装服务功能已禁用。请先在设置中点击“以管理员身份重启”。";
    }

    private ManagedService BuildManagedService()
    {
        var service = new ManagedService
        {
            Id = Draft.Id.Trim(),
            LogPolicy = LogPolicy.CreateDefault()
        };

        Draft.ApplyTo(service);
        return service;
    }

    private string BuildDefaultServiceId(string programName)
    {
        var raw = BuildDefaultText(programName, _paths.ServiceIdRuleMode, _paths.ServiceIdRulePrefix);
        return _idSuggester.SuggestFromRawName(raw);
    }

    private static string BuildDefaultText(string programName, string ruleMode, string prefix)
    {
        if (string.Equals(ruleMode, WsmPaths.DefaultRulePrefixProgramName, StringComparison.OrdinalIgnoreCase))
        {
            return (prefix ?? string.Empty) + programName;
        }

        return programName;
    }

    private void ResetForm()
    {
        Draft.PropertyChanged -= DraftOnPropertyChanged;
        Draft = new ServiceConfigDraft();
        ExecutablePathError = string.Empty;
        ServiceIdError = string.Empty;
        DisplayNameError = string.Empty;
        StopTimeoutError = string.Empty;
    }

    private void DraftOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ValidateAllFields(showSnackbar: false);
    }
}
