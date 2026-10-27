namespace BackendTray.Core.Tests;

public sealed class ServerCatalogTests
{
    [Fact]
    public void SkipsInvalidManifests()
    {
        using var env = new TestEnvironment()
            .WithServer("main-api", 5100)
            // 依資料夾名稱排序掃描，重複的 id 以先出現的為準。
            .WithManifest("z-duplicate", """{ "id": "main-api", "executable": "x.exe", "defaultPort": 5200 }""")
            .WithManifest("bad-id", """{ "id": "Bad Id", "executable": "x.exe", "defaultPort": 5300 }""")
            .WithManifest("reserved", """{ "id": "gateway", "executable": "x.exe", "defaultPort": 5400 }""")
            .WithManifest("broken", "{ not json");

        var servers = env.Catalog().Servers;

        var server = Assert.Single(servers);
        Assert.Equal("main-api", server.Id);
        Assert.Equal(Path.Combine(env.Paths.ServersDirectory, "main-api", "main-api.exe"), server.ExecutablePath);
    }
}
