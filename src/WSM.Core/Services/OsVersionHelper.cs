using System;

namespace WSM.Core.Services;

/// <summary>
/// 操作系统与运行时环境检测。
/// </summary>
public static class OsVersionHelper
{
    /// <summary>
    /// 是否为 Windows 7 SP1 或更高版本。
    /// </summary>
    public static bool IsWindows7Sp1OrLater()
    {
        return IsWindowsVersionAtLeast(6, 1);
    }

    /// <summary>
    /// 是否为 Windows 10 1607 或更高版本（Modern 版最低要求）。
    /// </summary>
    public static bool IsWindows10OrLater()
    {
        return IsWindowsVersionAtLeast(10, 0);
    }

    /// <summary>
    /// 获取可读的系统版本描述。
    /// </summary>
    public static string GetOsDescription()
    {
        return Environment.OSVersion.VersionString;
    }

    /// <summary>
    /// 检测 .NET Framework 4.8 是否已安装（仅 Windows 有效）。
    /// </summary>
    public static bool IsDotNetFramework48Installed()
    {
        if (!Environment.OSVersion.Platform.ToString().Contains("Win"))
        {
            return false;
        }

        try
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                       @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                if (key == null)
                {
                    return false;
                }

                var release = key.GetValue("Release");
                if (release == null)
                {
                    return false;
                }

                var releaseNumber = Convert.ToInt32(release, System.Globalization.CultureInfo.InvariantCulture);
                return releaseNumber >= 528040;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWindowsVersionAtLeast(int major, int minor)
    {
        var version = Environment.OSVersion.Version;
        if (version.Major > major)
        {
            return true;
        }

        return version.Major == major && version.Minor >= minor;
    }
}
