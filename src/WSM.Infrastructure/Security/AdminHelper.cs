using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;

namespace WSM.Infrastructure.Security;

/// <summary>
/// 管理员权限检测与可执行路径解析。
/// </summary>
public static class AdminHelper
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 是否通过 dotnet.exe 宿主启动（如 dotnet run）。
    /// </summary>
    public static bool IsRunningUnderDotnetHost()
    {
        var processPath = GetCurrentProcessPath();
        return IsDotnetExecutable(processPath);
    }

    /// <summary>
    /// 获取可用于 UAC 提权的应用 exe 路径。
    /// </summary>
    public static bool TryGetApplicationExecutable(out string executablePath)
    {
        executablePath = string.Empty;

        var processPath = GetCurrentProcessPath();
        if (!string.IsNullOrWhiteSpace(processPath)
            && processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && !IsDotnetExecutable(processPath)
            && File.Exists(processPath))
        {
            executablePath = processPath;
            return true;
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(entryAssembly.Location);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        foreach (var fileName in new[] { "WSM.exe", "WSM-Legacy.exe" })
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                executablePath = candidate;
                return true;
            }
        }

        return false;
    }

    private static string GetCurrentProcessPath()
    {
#if NET5_0_OR_GREATER
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }
#endif
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsDotnetExecutable(string path)
    {
        return path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase);
    }
}
