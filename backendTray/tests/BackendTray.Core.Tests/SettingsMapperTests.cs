using System.Text.Json;
using BackendTray.Contracts;
using BackendTray.Core.Settings;

namespace BackendTray.Core.Tests;

public sealed class SettingsMapperTests
{
    [Fact]
    public void ToDto_DoesNotExposePassword()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var settings = new AppSettings { SharedConnection = env.Connection("sql") };

        var dto = env.Mapper().ToDto(settings);

        Assert.True(dto.SharedConnection.HasPassword);
        Assert.Null(dto.SharedConnection.NewPassword);
        var json = JsonSerializer.Serialize(dto, ManagementJson.Options);
        Assert.DoesNotContain(settings.SharedConnection.ProtectedPassword!, json);
        Assert.DoesNotContain("secret", json);
    }

    [Fact]
    public void ToDto_ListsDeclaredConnectionsWithDefaults()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100, "Default", "Report");

        var server = env.Mapper().ToDto(new AppSettings()).Servers.Single();

        Assert.Equal(5100, server.Port);
        Assert.Equal(["Default", "Report"], server.Connections.Select(c => c.Name));
        Assert.All(server.Connections, connection => Assert.True(connection.UseShared));
    }

    [Fact]
    public void Apply_KeepsStoredPassword_WhenNewPasswordIsEmpty()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var mapper = env.Mapper();
        var current = new AppSettings { SharedConnection = env.Connection("sql", "old-password") };

        var dto = mapper.ToDto(current);
        dto.SharedConnection.Database = "OtherDb";
        var result = mapper.Apply(dto, current);

        Assert.Equal("OtherDb", result.SharedConnection.Database);
        Assert.Equal("old-password", env.Protector.Unprotect(result.SharedConnection.ProtectedPassword!));
    }

    [Fact]
    public void Apply_ReplacesPassword_WhenNewPasswordIsGiven()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var mapper = env.Mapper();
        var current = new AppSettings { SharedConnection = env.Connection("sql", "old-password") };

        var dto = mapper.ToDto(current);
        dto.SharedConnection.NewPassword = "new-password";
        var result = mapper.Apply(dto, current);

        Assert.Equal("new-password", env.Protector.Unprotect(result.SharedConnection.ProtectedPassword!));
    }

    [Fact]
    public void Apply_OwnConnectionThenBackToShared()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var mapper = env.Mapper();

        var dto = mapper.ToDto(new AppSettings());
        var connection = dto.Servers[0].Connections[0];
        connection.UseShared = false;
        connection.Connection = new DbConnectionDto { Server = "other-sql", Database = "Db", UserId = "app", NewPassword = "pw" };
        var withOwn = mapper.Apply(dto, new AppSettings());

        Assert.Equal("other-sql", withOwn.Servers["main-api"].Connections["Default"].Server);
        Assert.Equal("pw", mapper.GetStoredPassword(ConnectionSlots.ForServer("main-api", "Default"), withOwn));

        var back = mapper.ToDto(withOwn);
        back.Servers[0].Connections[0].UseShared = true;
        var withShared = mapper.Apply(back, withOwn);

        Assert.Empty(withShared.Servers["main-api"].Connections);
    }
}
