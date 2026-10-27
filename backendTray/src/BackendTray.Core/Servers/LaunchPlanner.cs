using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BackendTray.Contracts;
using BackendTray.Core.Database;
using BackendTray.Core.Settings;

namespace BackendTray.Core.Servers;

/// <summary>把設定與 server.json 轉成各子程序的啟動資訊（環境變數、port、Gateway 路由）。</summary>
public sealed class LaunchPlanner(AppPaths paths, ConnectionStringFactory connectionStrings)
{
    public const string ManagedVariable = "BACKENDTRAY_MANAGED";
    public const string ServerIdVariable = "BACKENDTRAY_SERVER_ID";

    public LaunchPlan Create(AppSettings settings, IReadOnlyList<ServerManifest> manifests)
    {
        var warnings = new List<string>();
        var apis = manifests.Select(manifest => CreateApi(manifest, settings, warnings)).ToList();

        var gatewayPort = settings.Gateway.Port;
        var gateway = new LaunchSpec
        {
            Id = ManagementConstants.GatewayServerId,
            DisplayName = "前端 Gateway",
            Kind = ServerKind.Gateway,
            ExecutablePath = paths.GatewayExecutable,
            Arguments = ["--config", paths.GatewayConfigFile],
            Environment = CommonEnvironment(ManagementConstants.GatewayServerId, gatewayPort, settings.Gateway.Listen),
            Port = gatewayPort,
            Listen = settings.Gateway.Listen,
            HealthUrl = $"http://127.0.0.1:{gatewayPort}/_gateway/health",
            ConfigFilePath = paths.GatewayConfigFile,
            ConfigFileContent = BuildGatewayConfig(paths.WebRoot, manifests, apis),
        };

        return new LaunchPlan(gateway, apis, warnings);
    }

    private LaunchSpec CreateApi(ServerManifest manifest, AppSettings settings, List<string> warnings)
    {
        var serverSettings = settings.Servers.GetValueOrDefault(manifest.Id) ?? new ServerSettings();
        var port = serverSettings.Port ?? manifest.DefaultPort;
        var environment = CommonEnvironment(manifest.Id, port, serverSettings.Listen);

        foreach (var name in manifest.Connections)
        {
            var connection = serverSettings.Connections.GetValueOrDefault(name) ?? settings.SharedConnection;
            if (!connection.IsConfigured)
            {
                if (serverSettings.Enabled)
                    warnings.Add($"{manifest.DisplayName} 的資料庫連線「{name}」尚未設定。");
                continue;
            }

            try
            {
                environment[$"ConnectionStrings__{name}"] = connectionStrings.Build(connection, $"BackendTray/{manifest.Id}");
            }
            catch (CryptographicException)
            {
                warnings.Add($"{manifest.DisplayName} 的資料庫連線「{name}」密碼無法解密，請重新輸入密碼。");
            }
        }

        return new LaunchSpec
        {
            Id = manifest.Id,
            DisplayName = manifest.DisplayName,
            Kind = ServerKind.Api,
            ExecutablePath = manifest.ExecutablePath,
            Environment = environment,
            Port = port,
            Listen = serverSettings.Listen,
            HealthUrl = $"http://127.0.0.1:{port}{manifest.HealthPath}",
            Enabled = serverSettings.Enabled,
        };
    }

    private static Dictionary<string, string> CommonEnvironment(string serverId, int port, ListenScope listen) => new()
    {
        ["ASPNETCORE_URLS"] = listen == ListenScope.Any ? $"http://*:{port}" : $"http://localhost:{port}",
        [ManagedVariable] = "1",
        [ServerIdVariable] = serverId,
        ["Logging__Console__FormatterOptions__SingleLine"] = "true",
    };

    /// <summary>Gateway 的設定：前端檔案位置，以及每個啟用中 API 的 YARP 路由。</summary>
    private static string BuildGatewayConfig(string webRoot, IReadOnlyList<ServerManifest> manifests, IReadOnlyList<LaunchSpec> apis)
    {
        var routes = new JsonObject();
        var clusters = new JsonObject();

        foreach (var api in apis.Where(api => api.Enabled))
        {
            var manifest = manifests.First(m => m.Id == api.Id);
            routes[api.Id] = new JsonObject
            {
                ["ClusterId"] = api.Id,
                ["Match"] = new JsonObject { ["Path"] = manifest.RoutePrefix.TrimEnd('/') + "/{**catch-all}" },
            };
            clusters[api.Id] = new JsonObject
            {
                ["Destinations"] = new JsonObject
                {
                    ["primary"] = new JsonObject { ["Address"] = $"http://127.0.0.1:{api.Port}/" },
                },
            };
        }

        var root = new JsonObject
        {
            ["Gateway"] = new JsonObject { ["WebRoot"] = webRoot },
            ["ReverseProxy"] = new JsonObject { ["Routes"] = routes, ["Clusters"] = clusters },
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
