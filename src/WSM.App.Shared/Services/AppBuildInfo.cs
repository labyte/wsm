using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace WSM.App.Shared.Services;

/// <summary>
/// 应用版本、构建日期与程序指纹（用于关于页展示）。
/// </summary>
public static class AppBuildInfo
{
    public static string ResolveCopyrightText()
        => $"Copyright © {DateTime.Now.Year} Huari Ltd.";

    public static string ResolveVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            var infoVersion = entryAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                return TrimBuildMetadata(infoVersion!.Trim());
            }

            var assemblyVersion = entryAssembly.GetName().Version?.ToString();
            if (!string.IsNullOrWhiteSpace(assemblyVersion))
            {
                return assemblyVersion!;
            }
        }

        var sharedVersion = typeof(AppBuildInfo).Assembly.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(sharedVersion) ? "0.1.0" : sharedVersion!;
    }

    public static string ResolveBuildDateText()
    {
        var path = ResolveEntryLocation();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "-";
        }

        var timestamp = File.GetLastWriteTime(path);
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static string ResolveFingerprint()
    {
        var path = ResolveEntryLocation();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "-";
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
        catch
        {
            return "-";
        }
    }

    private static string? ResolveEntryLocation()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
        {
            return null;
        }

        var location = entryAssembly.Location;
        if (!string.IsNullOrWhiteSpace(location))
        {
            return location;
        }

        return AppContext.BaseDirectory;
    }

    private static string TrimBuildMetadata(string informationalVersion)
    {
        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 ? informationalVersion.Substring(0, plusIndex) : informationalVersion;
    }
}
