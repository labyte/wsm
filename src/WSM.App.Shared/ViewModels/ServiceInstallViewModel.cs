using System;
using System.Collections.Generic;
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
/// 添加服务向导 ViewModel。
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
    private int _currentStep;

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
    private FailurePolicyTemplate _failurePolicyTemplate = FailurePolicyTemplate.Standard;

    [ObservableProperty]
    private bool _isInstalling;

    public IReadOnlyList<string> StepTitles { get; } = new[]
    {
        "选择程序",
        "基本信息",
        "运行与启动",
        "守护与日志"
    };

    public Array StartModeOptions => Enum.GetValues(typeof(ManagedServiceStartMode));

    public Array FailurePolicyTemplates => Enum.GetValues(typeof(FailurePolicyTemplate));

    public string CurrentStepTitle => CurrentStep >= 0 && CurrentStep < StepTitles.Count
        ? StepTitles[CurrentStep]
        : string.Empty;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStepTitle));
    }

    public void OnNavigatedTo()
    {
        if (CurrentStep == 0 && string.IsNullOrWhiteSpace(ExecutablePath))
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
            ServiceId = _idSuggester.SuggestFromExecutablePath(dialog.FileName);
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    [RelayCommand]
    private void NextStep()
    {
        if (!ValidateCurrentStep())
        {
            return;
        }

        if (CurrentStep < StepTitles.Count - 1)
        {
            CurrentStep++;
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (!ValidateAllSteps())
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

    private bool ValidateAllSteps()
    {
        for (var step = 0; step < StepTitles.Count; step++)
        {
            var previous = CurrentStep;
            CurrentStep = step;
            if (!ValidateCurrentStep())
            {
                return false;
            }

            CurrentStep = previous;
        }

        CurrentStep = StepTitles.Count - 1;
        return true;
    }

    private bool ValidateCurrentStep()
    {
        switch (CurrentStep)
        {
            case 0:
                if (string.IsNullOrWhiteSpace(ExecutablePath) || !File.Exists(ExecutablePath))
                {
                    _snackbarService.ShowWarning("请选择有效的可执行文件。");
                    return false;
                }

                return true;

            case 1:
                if (string.IsNullOrWhiteSpace(ServiceId) || !_validator.IsValidIdFormat(ServiceId))
                {
                    _snackbarService.ShowWarning("服务 ID 格式无效。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(DisplayName))
                {
                    _snackbarService.ShowWarning("显示名称不能为空。");
                    return false;
                }

                return true;

            case 2:
                if (StopTimeoutSeconds <= 0)
                {
                    _snackbarService.ShowWarning("停止超时必须大于 0。");
                    return false;
                }

                return true;

            default:
                return true;
        }
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
            FailurePolicy = FailurePolicy.CreateFromTemplate(FailurePolicyTemplate),
            LogPolicy = LogPolicy.CreateDefault()
        };
    }

    private void ResetForm()
    {
        CurrentStep = 0;
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
        FailurePolicyTemplate = FailurePolicyTemplate.Standard;
    }
}
