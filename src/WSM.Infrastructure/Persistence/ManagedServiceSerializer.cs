using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WSM.Core.Models;

namespace WSM.Infrastructure.Persistence;

/// <summary>
/// ManagedService JSON 序列化。
/// </summary>
public static class ManagedServiceSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(ManagedService service)
    {
        return JsonSerializer.Serialize(service, Options);
    }

    public static ManagedService Deserialize(string json)
    {
        var service = JsonSerializer.Deserialize<ManagedService>(json, Options);
        if (service == null)
        {
            throw new InvalidOperationException("无法反序列化服务配置。");
        }

        return service;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }
}
