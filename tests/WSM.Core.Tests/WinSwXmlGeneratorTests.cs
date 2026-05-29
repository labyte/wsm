using System;
using System.IO;
using WSM.Core.Models;
using WSM.Core.Services;
using Xunit;

namespace WSM.Core.Tests;

public class WinSwXmlGeneratorTests
{
    private readonly WinSwXmlGenerator _generator = new WinSwXmlGenerator();

    [Fact]
    public void GenerateUtf8Bytes_StartsWithServiceElement_NoBom()
    {
        var service = CreateSampleService();
        var bytes = _generator.GenerateUtf8Bytes(service);

        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'<', bytes[0]);
        Assert.NotEqual(0xEF, bytes[0]);

        var xml = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("<service>", xml.TrimStart());
    }

    [Fact]
    public void Generate_IncludesRequiredWinSwElements()
    {
        var service = CreateSampleService();

        var xml = _generator.Generate(service);

        Assert.Contains("<id>my-api</id>", xml);
        Assert.Contains("<name>My API</name>", xml);
        Assert.Contains("<executable>C:\\apps\\my-api.exe</executable>", xml);
        Assert.Contains("<startmode>Automatic</startmode>", xml);
        Assert.Contains("<delayedAutoStart", xml);
        Assert.DoesNotContain("<delayedAutoStart>true</delayedAutoStart>", xml);
        Assert.Contains("mode=\"roll-by-size\"", xml);
        Assert.Contains("action=\"restart\"", xml);
        Assert.Contains("delay=\"5 sec\"", xml);
        Assert.Contains("delay=\"10 sec\"", xml);
        Assert.Contains("action=\"none\"", xml);
        Assert.Contains("<resetfailure>1 hour</resetfailure>", xml);
        Assert.Contains("<stoptimeout>15sec</stoptimeout>", xml);
    }

    [Fact]
    public void Generate_IncludesEnvironmentAndDependencies()
    {
        var service = CreateSampleService();
        service.EnvironmentVariables.Add(new EnvVariable { Name = "ASPNETCORE_ENVIRONMENT", Value = "Production" });
        service.Dependencies.Add("EventLog");

        var xml = _generator.Generate(service);

        Assert.Contains("name=\"ASPNETCORE_ENVIRONMENT\"", xml);
        Assert.Contains("value=\"Production\"", xml);
        Assert.Contains("<depend>EventLog</depend>", xml);
    }

    private static ManagedService CreateSampleService()
    {
        return new ManagedService
        {
            Id = "my-api",
            DisplayName = "My API",
            Description = "Test service",
            ExecutablePath = @"C:\apps\my-api.exe",
            WorkingDirectory = @"C:\apps",
            Arguments = "--port 8080",
            StartMode = ManagedServiceStartMode.Automatic,
            DelayedAutoStart = true,
            StopTimeoutSeconds = 15,
            FailurePolicy = FailurePolicy.CreateStandard(),
            LogPolicy = LogPolicy.CreateDefault()
        };
    }
}
