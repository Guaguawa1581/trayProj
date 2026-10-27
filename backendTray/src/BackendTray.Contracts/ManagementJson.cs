using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendTray.Contracts;

/// <summary>管理 API 兩端共用的 JSON 格式（camelCase、列舉以字串表示）。</summary>
public static class ManagementJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
