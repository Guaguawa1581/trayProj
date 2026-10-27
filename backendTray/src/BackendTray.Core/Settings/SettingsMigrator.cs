using System.Text.Json.Nodes;

namespace BackendTray.Core.Settings;

/// <summary>把舊版 settings.json 轉成目前的格式，讓更新後可以沿用原本的設定。</summary>
internal static class SettingsMigrator
{
    /// <summary>索引鍵是來源版本：[1] 負責把 v1 轉成 v2，依此類推。</summary>
    private static readonly Dictionary<int, Action<JsonObject>> Steps = new()
    {
    };

    public static void Migrate(JsonObject root)
    {
        var version = root["schemaVersion"]?.GetValue<int>() ?? 1;

        for (; version < AppSettings.CurrentSchemaVersion; version++)
        {
            if (!Steps.TryGetValue(version, out var step))
                throw new NotSupportedException($"缺少設定檔 v{version} → v{version + 1} 的轉換。");
            step(root);
        }

        root["schemaVersion"] = version;
    }
}
