using System.Text.Json.Nodes;
using BackendTray.Contracts;
using BackendTray.Core.Settings;

namespace BackendTray.Core.Tests;

public sealed class LaunchPlannerTests
{
    [Fact]
    public void Api_UsesSharedConnection_WhenNotOverridden()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var settings = new AppSettings { SharedConnection = env.Connection("shared-sql") };

        var api = env.Planner().Create(settings, env.Catalog().Servers).Apis.Single();

        Assert.Contains("Data Source=shared-sql", api.Environment["ConnectionStrings__Default"]);
        Assert.Contains("Password=secret", api.Environment["ConnectionStrings__Default"]);
    }

    [Fact]
    public void Api_UsesOwnConnection_WhenOverridden()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100, "Default", "Report");
        var settings = new AppSettings { SharedConnection = env.Connection("shared-sql") };
        settings.Servers["main-api"] = new ServerSettings { Connections = { ["Report"] = env.Connection("report-sql") } };

        var api = env.Planner().Create(settings, env.Catalog().Servers).Apis.Single();

        Assert.Contains("Data Source=shared-sql", api.Environment["ConnectionStrings__Default"]);
        Assert.Contains("Data Source=report-sql", api.Environment["ConnectionStrings__Report"]);
    }

    [Fact]
    public void Api_WithoutConnection_ProducesWarning()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);

        var plan = env.Planner().Create(new AppSettings(), env.Catalog().Servers);

        Assert.DoesNotContain("ConnectionStrings__Default", plan.Apis.Single().Environment.Keys);
        Assert.Single(plan.Warnings);
    }

    [Theory]
    [InlineData(ListenScope.Localhost, "http://localhost:5100")]
    [InlineData(ListenScope.Any, "http://*:5100")]
    public void ListenScope_ControlsUrls(ListenScope listen, string expected)
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var settings = new AppSettings();
        settings.Servers["main-api"] = new ServerSettings { Listen = listen };

        var api = env.Planner().Create(settings, env.Catalog().Servers).Apis.Single();

        Assert.Equal(expected, api.Environment["ASPNETCORE_URLS"]);
        Assert.Equal("1", api.Environment["BACKENDTRAY_MANAGED"]);
    }

    [Fact]
    public void GatewayConfig_RoutesOnlyEnabledApis()
    {
        using var env = new TestEnvironment().WithServer("orders", 5100).WithServer("reports", 5200);
        var settings = new AppSettings();
        settings.Servers["orders"] = new ServerSettings { Port = 5150 };
        settings.Servers["reports"] = new ServerSettings { Enabled = false };

        var gateway = env.Planner().Create(settings, env.Catalog().Servers).Gateway;

        var config = JsonNode.Parse(gateway.ConfigFileContent!)!;
        var routes = config["ReverseProxy"]!["Routes"]!.AsObject();
        Assert.Equal(["orders"], routes.Select(route => route.Key));
        Assert.Equal("/orders/{**catch-all}", (string?)routes["orders"]!["Match"]!["Path"]);
        Assert.Equal("http://127.0.0.1:5150/", (string?)config["ReverseProxy"]!["Clusters"]!["orders"]!["Destinations"]!["primary"]!["Address"]);
        Assert.Equal(env.Paths.WebRoot, (string?)config["Gateway"]!["WebRoot"]);
    }

    [Fact]
    public void Fingerprint_ChangesOnlyForAffectedServers()
    {
        using var env = new TestEnvironment().WithServer("orders", 5100).WithServer("reports", 5200);
        var planner = env.Planner();
        var before = planner.Create(new AppSettings(), env.Catalog().Servers);

        var settings = new AppSettings();
        settings.Servers["orders"] = new ServerSettings { Port = 5150 };
        var after = planner.Create(settings, env.Catalog().Servers);

        Assert.NotEqual(before.Apis[0].Fingerprint, after.Apis[0].Fingerprint);
        Assert.Equal(before.Apis[1].Fingerprint, after.Apis[1].Fingerprint);
        // Gateway 的路由指向新的 port，所以也要重新啟動。
        Assert.NotEqual(before.Gateway.Fingerprint, after.Gateway.Fingerprint);
    }
}
