using WSM.Core.Models;
using WSM.Core.Services;
using Xunit;

namespace WSM.Core.Tests;

public class LogLevelParserTests
{
    private readonly LogLevelParser _parser = new LogLevelParser();

    [Theory]
    [InlineData("2024-01-01 INFO  - Service started", LogSource.Wrapper, LogLevel.Info)]
    [InlineData("2024-01-01 WARN  - Low memory", LogSource.Wrapper, LogLevel.Warning)]
    [InlineData("2024-01-01 ERROR - Failed", LogSource.Wrapper, LogLevel.Error)]
    [InlineData("2024-01-01 FATAL - Crash", LogSource.Wrapper, LogLevel.Fatal)]
    [InlineData("[INFO] Request handled", LogSource.StdOut, LogLevel.Info)]
    [InlineData("WARN | Something happened", LogSource.StdOut, LogLevel.Warning)]
    [InlineData("{\"level\":\"error\",\"msg\":\"fail\"}", LogSource.StdOut, LogLevel.Error)]
    [InlineData("plain output", LogSource.StdOut, LogLevel.Debug)]
    [InlineData("stderr message", LogSource.StdErr, LogLevel.Error)]
    public void ParseLevel_DetectsExpectedLevel(string line, LogSource source, LogLevel expected)
    {
        var level = _parser.ParseLevel(line, source);

        Assert.Equal(expected, level);
    }
}
