using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace BackendTray.Core.Settings;

/// <summary>settings.json 的讀寫。服務是唯一的寫入者；每次讀寫都回傳副本。</summary>
public sealed class SettingsStore(AppPaths paths, ILogger<SettingsStore> logger)
{
    private readonly Lock _sync = new();
    private AppSettings? _current;

    public AppSettings Load()
    {
        lock (_sync)
        {
            _current ??= ReadFromDisk();
            return SettingsJson.Clone(_current);
        }
    }

    /// <summary>寫入設定並把 Revision 加一，回傳實際儲存的內容。</summary>
    public AppSettings Save(AppSettings settings)
    {
        lock (_sync)
        {
            var saved = SettingsJson.Clone(settings);
            saved.SchemaVersion = AppSettings.CurrentSchemaVersion;
            saved.Revision = (_current ?? ReadFromDisk()).Revision + 1;
            AtomicFile.WriteAllText(paths.SettingsFile, SettingsJson.Serialize(saved));
            _current = saved;
            return SettingsJson.Clone(saved);
        }
    }

    private AppSettings ReadFromDisk()
    {
        var path = paths.SettingsFile;
        if (!File.Exists(path))
            return new AppSettings();

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new JsonException("設定檔的根節點不是物件。");
            SettingsMigrator.Migrate(root);
            return root.Deserialize<AppSettings>(SettingsJson.Options) ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            // 檔案毀損時保留原檔供查看，改用預設值讓服務仍能啟動。
            var backup = $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(path, backup);
            logger.LogError(ex, "設定檔格式錯誤，已改名為 {Backup} 並改用預設設定。", backup);
            return new AppSettings();
        }
    }
}
