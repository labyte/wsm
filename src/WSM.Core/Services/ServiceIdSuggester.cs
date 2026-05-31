using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WSM.Core.Services;

/// <summary>
/// 根据可执行文件路径建议服务 ID。
/// </summary>
public sealed class ServiceIdSuggester
{
    private static readonly Regex InvalidCharPattern = new Regex(
        "[^a-z0-9-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 从 exe 路径生成建议的服务 ID。
    /// </summary>
    public string SuggestFromExecutablePath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return "service";
        }

        var fileName = Path.GetFileNameWithoutExtension(executablePath) ?? "service";
        return SuggestFromRawName(fileName);
    }

    /// <summary>
    /// 从原始文本生成服务 ID（会进行格式标准化）。
    /// </summary>
    public string SuggestFromRawName(string? rawName)
    {
        var normalized = (rawName ?? string.Empty).Trim().ToLowerInvariant();
        normalized = InvalidCharPattern.Replace(normalized, "-");
        normalized = normalized.Trim('-');

        while (normalized.Contains("--"))
        {
            normalized = normalized.Replace("--", "-");
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "service";
        }

        if (!char.IsLetter(normalized[0]))
        {
            normalized = "svc-" + normalized;
        }

        return normalized;
    }
}
