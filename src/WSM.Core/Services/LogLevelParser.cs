using System;
using System.Text.RegularExpressions;
using WSM.Core.Interfaces;
using WSM.Core.Models;

namespace WSM.Core.Services;

/// <summary>
/// 日志级别解析器，支持 Wrapper 日志与常见应用格式。
/// </summary>
public sealed class LogLevelParser : ILogParser
{
    private static readonly Regex WrapperLevelPattern = new Regex(
        @"\b(TRACE|DEBUG|INFO|WARN|WARNING|ERROR|ERR|FATAL)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BracketLevelPattern = new Regex(
        @"\[(TRACE|DEBUG|INFO|WARN|WARNING|ERROR|ERR|FATAL)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PipeLevelPattern = new Regex(
        @"^\s*(TRACE|DEBUG|INFO|WARN|WARNING|ERROR|ERR|FATAL)\s*\|",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex JsonLevelPattern = new Regex(
        @"""level""\s*:\s*""(?<level>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public LogLevel ParseLevel(string line, LogSource source)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return LogLevel.Unknown;
        }

        if (source == LogSource.StdErr)
        {
            return LogLevel.Error;
        }

        if (source == LogSource.Wrapper)
        {
            return ParseToken(MatchWrapperLevel(line));
        }

        var bracketMatch = BracketLevelPattern.Match(line);
        if (bracketMatch.Success)
        {
            return ParseToken(bracketMatch.Groups[1].Value);
        }

        var pipeMatch = PipeLevelPattern.Match(line);
        if (pipeMatch.Success)
        {
            return ParseToken(pipeMatch.Groups[1].Value);
        }

        var jsonMatch = JsonLevelPattern.Match(line);
        if (jsonMatch.Success)
        {
            return ParseToken(jsonMatch.Groups["level"].Value);
        }

        var genericMatch = WrapperLevelPattern.Match(line);
        if (genericMatch.Success)
        {
            return ParseToken(genericMatch.Groups[1].Value);
        }

        return LogLevel.Debug;
    }

    private static string MatchWrapperLevel(string line)
    {
        var match = WrapperLevelPattern.Match(line);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static LogLevel ParseToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return LogLevel.Unknown;
        }

        switch (token.Trim().ToUpperInvariant())
        {
            case "TRACE":
                return LogLevel.Trace;
            case "DEBUG":
                return LogLevel.Debug;
            case "INFO":
                return LogLevel.Info;
            case "WARN":
            case "WARNING":
                return LogLevel.Warning;
            case "ERROR":
            case "ERR":
                return LogLevel.Error;
            case "FATAL":
                return LogLevel.Fatal;
            default:
                return LogLevel.Unknown;
        }
    }
}
