using System.Text.Json;
using BackendTray.Contracts;

namespace BackendTray.Core.Settings;

internal static class SettingsJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(Serialize(value), Options)!;

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        ManagementJson.Configure(options);
        return options;
    }
}
