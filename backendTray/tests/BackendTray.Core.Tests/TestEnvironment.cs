using BackendTray.Core;
using BackendTray.Core.Database;
using BackendTray.Core.Servers;
using BackendTray.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace BackendTray.Core.Tests;

/// <summary>在暫存資料夾建立安裝目錄（含 servers\*\server.json）與資料目錄。</summary>
internal sealed class TestEnvironment : IDisposable
{
    public TestEnvironment()
    {
        Root = Path.Combine(Path.GetTempPath(), "BackendTray.Tests", Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(Path.Combine(Root, "install"), Path.Combine(Root, "data"));
        Directory.CreateDirectory(Paths.ServersDirectory);
    }

    public string Root { get; }
    public AppPaths Paths { get; }
    public SecretProtector Protector { get; } = new();

    public TestEnvironment WithServer(string id, int defaultPort, params string[] connections)
    {
        var connectionList = string.Join(", ", (connections.Length == 0 ? ["Default"] : connections).Select(c => $"\"{c}\""));
        return WithManifest(id, $$"""
            {
              "id": "{{id}}",
              "displayName": "API {{id}}",
              "executable": "{{id}}.exe",
              "defaultPort": {{defaultPort}},
              "routePrefix": "/{{id}}",
              "connections": [ {{connectionList}} ]
            }
            """);
    }

    public TestEnvironment WithManifest(string folder, string json)
    {
        var directory = Path.Combine(Paths.ServersDirectory, folder);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "server.json"), json);
        return this;
    }

    public ServerCatalog Catalog() => new(Paths, NullLogger<ServerCatalog>.Instance);

    public SettingsStore Store() => new(Paths, NullLogger<SettingsStore>.Instance);

    public LaunchPlanner Planner() => new(Paths, new ConnectionStringFactory(Protector));

    public SettingsMapper Mapper() => new(Catalog(), Protector);

    public DbConnectionSettings Connection(string server, string password = "secret") => new()
    {
        Server = server,
        Database = "AppDb",
        UserId = "app",
        ProtectedPassword = Protector.Protect(password),
    };

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
