using BackendTray.Core.Settings;

namespace BackendTray.Core.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Load_WithoutFile_ReturnsDefaults()
    {
        using var env = new TestEnvironment();

        var settings = env.Store().Load();

        Assert.Equal(0, settings.Revision);
        Assert.Equal(8080, settings.Gateway.Port);
        Assert.False(settings.SharedConnection.IsConfigured);
    }

    [Fact]
    public void Save_IncrementsRevisionAndPersists()
    {
        using var env = new TestEnvironment();
        var store = env.Store();

        var settings = store.Load();
        settings.Gateway.Port = 9090;
        store.Save(settings);
        var second = store.Save(store.Load());

        var reloaded = env.Store().Load();
        Assert.Equal(2, second.Revision);
        Assert.Equal(2, reloaded.Revision);
        Assert.Equal(9090, reloaded.Gateway.Port);
        Assert.False(File.Exists(env.Paths.SettingsFile + ".tmp"));
    }

    [Fact]
    public void Load_ReturnsCopy()
    {
        using var env = new TestEnvironment();
        var store = env.Store();

        store.Load().Gateway.Port = 1234;

        Assert.Equal(8080, store.Load().Gateway.Port);
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaultsAndKeepsBackup()
    {
        using var env = new TestEnvironment();
        Directory.CreateDirectory(env.Paths.DataRoot);
        File.WriteAllText(env.Paths.SettingsFile, "{ not json");

        var settings = env.Store().Load();

        Assert.Equal(8080, settings.Gateway.Port);
        Assert.False(File.Exists(env.Paths.SettingsFile));
        Assert.Single(Directory.GetFiles(env.Paths.DataRoot, "settings.json.corrupt-*"));
    }
}
