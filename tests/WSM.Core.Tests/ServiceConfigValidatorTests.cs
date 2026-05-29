using System.Collections.Generic;
using System.IO;
using WSM.Core.Models;
using WSM.Core.Services;
using Xunit;

namespace WSM.Core.Tests;

public class ServiceConfigValidatorTests
{
    private readonly ServiceConfigValidator _validator = new ServiceConfigValidator();

    [Fact]
    public void Validate_ValidService_Passes()
    {
        var service = CreateValidService();

        var result = _validator.Validate(service);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidId_Fails()
    {
        var service = CreateValidService();
        service.Id = "MyService";

        var result = _validator.Validate(service);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ManagedService.Id));
    }

    [Fact]
    public void Validate_DuplicateId_Fails()
    {
        var service = CreateValidService();
        var existing = new List<string> { "my-api" };

        var result = _validator.Validate(service, existing);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EditMode_AllowsSameId()
    {
        var service = CreateValidService();
        var existing = new List<string> { "my-api" };

        var result = _validator.Validate(service, existing, excludeServiceId: "my-api");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingExecutable_Fails()
    {
        var service = CreateValidService();
        service.ExecutablePath = @"C:\not-exists\missing.exe";

        var result = _validator.Validate(service);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ManagedService.ExecutablePath));
    }

    [Theory]
    [InlineData("my-api", true)]
    [InlineData("a1", true)]
    [InlineData("MyApi", false)]
    [InlineData("1abc", false)]
    [InlineData("", false)]
    public void IsValidIdFormat_Works(string id, bool expected)
    {
        Assert.Equal(expected, _validator.IsValidIdFormat(id));
    }

    private static ManagedService CreateValidService()
    {
        var exePath = Path.Combine(Environment.SystemDirectory, "notepad.exe");

        return new ManagedService
        {
            Id = "my-api",
            DisplayName = "My API",
            ExecutablePath = exePath,
            WorkingDirectory = Environment.SystemDirectory,
            FailurePolicy = FailurePolicy.CreateStandard(),
            LogPolicy = LogPolicy.CreateDefault()
        };
    }
}
