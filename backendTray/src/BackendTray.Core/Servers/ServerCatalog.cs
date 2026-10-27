using System.Text.Json;
using System.Text.RegularExpressions;
using BackendTray.Contracts;
using BackendTray.Core.Settings;
using Microsoft.Extensions.Logging;

namespace BackendTray.Core.Servers;

/// <summary>掃描 servers\*\server.json。清單只在安裝或更新時改變，所以讀一次後快取。</summary>
public sealed partial class ServerCatalog(AppPaths paths, ILogger<ServerCatalog> logger)
{
    private IReadOnlyList<ServerManifest>? _servers;

    public IReadOnlyList<ServerManifest> Servers => _servers ??= Scan();

    public ServerManifest? Find(string id) =>
        Servers.FirstOrDefault(server => string.Equals(server.Id, id, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<ServerManifest> Scan()
    {
        var result = new List<ServerManifest>();
        if (!Directory.Exists(paths.ServersDirectory))
        {
            logger.LogWarning("找不到 API 資料夾 {Directory}", paths.ServersDirectory);
            return result;
        }

        foreach (var directory in Directory.GetDirectories(paths.ServersDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            var file = Path.Combine(directory, "server.json");
            if (!File.Exists(file))
                continue;

            try
            {
                var manifest = JsonSerializer.Deserialize<ServerManifest>(File.ReadAllText(file), SettingsJson.Options)
                    ?? throw new JsonException("內容是空的。");
                manifest.Directory = directory;

                var error = Validate(manifest, result);
                if (error is null)
                    result.Add(manifest);
                else
                    logger.LogError("略過 {File}：{Error}", file, error);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "略過 {File}：格式錯誤", file);
            }
        }

        return result;
    }

    private static string? Validate(ServerManifest manifest, IReadOnlyList<ServerManifest> accepted)
    {
        if (!IdPattern().IsMatch(manifest.Id))
            return $"id「{manifest.Id}」只能使用小寫英數與連字號。";
        if (string.Equals(manifest.Id, ManagementConstants.GatewayServerId, StringComparison.OrdinalIgnoreCase))
            return $"id 不能是保留字「{ManagementConstants.GatewayServerId}」。";
        if (accepted.Any(other => string.Equals(other.Id, manifest.Id, StringComparison.OrdinalIgnoreCase)))
            return $"id「{manifest.Id}」重複。";
        if (manifest.DefaultPort is < 1 or > 65535)
            return "defaultPort 必須在 1–65535 之間。";
        if (!manifest.RoutePrefix.StartsWith('/'))
            return "routePrefix 必須以 / 開頭。";
        if (!manifest.HealthPath.StartsWith('/'))
            return "healthPath 必須以 / 開頭。";
        if (string.IsNullOrWhiteSpace(manifest.Executable))
            return "缺少 executable。";
        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
            manifest.DisplayName = manifest.Id;
        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex IdPattern();
}
