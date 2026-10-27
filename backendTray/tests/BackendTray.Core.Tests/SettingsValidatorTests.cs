using BackendTray.Core.Settings;

namespace BackendTray.Core.Tests;

public sealed class SettingsValidatorTests
{
    private static readonly IReadOnlySet<int> NoPorts = new HashSet<int>();

    [Fact]
    public void DuplicatePorts_AreRejected()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var proposed = new AppSettings { Gateway = { Port = 5100 } };

        var errors = SettingsValidator.Validate(proposed, new AppSettings(), env.Catalog().Servers, NoPorts, _ => false);

        Assert.Contains(errors, error => error.Contains("5100") && error.Contains("重複"));
    }

    [Fact]
    public void ChangedPortUsedByAnotherProgram_IsRejected()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var proposed = new AppSettings { Gateway = { Port = 9000 } };

        var errors = SettingsValidator.Validate(proposed, new AppSettings(), env.Catalog().Servers, NoPorts, port => port == 9000);

        Assert.Contains(errors, error => error.Contains("9000") && error.Contains("已被其他程式使用"));
    }

    [Fact]
    public void UnchangedOrOwnPorts_AreNotCheckedForConflicts()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var proposed = new AppSettings();
        proposed.Servers["main-api"] = new ServerSettings { Port = 5200 };

        var errors = SettingsValidator.Validate(proposed, new AppSettings(), env.Catalog().Servers, new HashSet<int> { 5200 }, _ => true);

        Assert.Empty(errors);
    }

    [Fact]
    public void OwnConnectionWithoutPassword_IsRejected()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);
        var proposed = new AppSettings();
        proposed.Servers["main-api"] = new ServerSettings
        {
            Connections = { ["Default"] = new DbConnectionSettings { Server = "sql", Database = "Db", UserId = "app" } },
        };

        var errors = SettingsValidator.Validate(proposed, new AppSettings(), env.Catalog().Servers, NoPorts, _ => false);

        Assert.Contains(errors, error => error.Contains("請輸入密碼"));
    }

    [Fact]
    public void EmptySharedConnection_IsAllowed()
    {
        using var env = new TestEnvironment().WithServer("main-api", 5100);

        var errors = SettingsValidator.Validate(new AppSettings(), new AppSettings(), env.Catalog().Servers, NoPorts, _ => false);

        Assert.Empty(errors);
    }
}
