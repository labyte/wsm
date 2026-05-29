using System.Diagnostics;
using System.Windows;
using WSM.Infrastructure.Security;

namespace WSM.App.Shared.Services;

/// <summary>
/// 管理员权限提升服务。
/// </summary>
public sealed class AdminElevationService
{
    public bool IsRunningAsAdministrator => AdminHelper.IsRunningAsAdministrator();

    public bool IsRunningUnderDotnetHost => AdminHelper.IsRunningUnderDotnetHost();

    public bool CanRestartElevated => AdminHelper.TryGetApplicationExecutable(out _);

    public string GetAdminStatusText()
    {
        if (IsRunningAsAdministrator)
        {
            return "当前已以管理员身份运行。";
        }

        if (IsRunningUnderDotnetHost)
        {
            return "当前通过 dotnet run 启动，未继承管理员权限。";
        }

        return "当前未以管理员身份运行，安装/管理服务需要提升权限。";
    }

    /// <summary>
    /// 触发 UAC 并以管理员身份重启应用。
    /// </summary>
    public bool TryRestartAsAdministrator()
    {
        if (!AdminHelper.TryGetApplicationExecutable(out var executablePath))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            Verb = "runas"
        });

        Application.Current.Shutdown();
        return true;
    }
}
