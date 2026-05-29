using WSM.Core.Services;
using Xunit;

namespace WSM.Core.Tests;

public class ServiceIdSuggesterTests
{
    private readonly ServiceIdSuggester _suggester = new ServiceIdSuggester();

    [Theory]
    [InlineData(@"C:\Apps\MyApi.exe", "myapi")]
    [InlineData(@"C:\Apps\My API Server.exe", "my-api-server")]
    [InlineData(@"C:\Apps\123.exe", "svc-123")]
    [InlineData("", "service")]
    public void SuggestFromExecutablePath_NormalizesFileName(string path, string expected)
    {
        var id = _suggester.SuggestFromExecutablePath(path);

        Assert.Equal(expected, id);
    }
}
