using System.Text.Json.Serialization;
using BackendTray.Contracts;

namespace BackendTray.Core.Settings;

/// <summary>
/// settings.json 的內容。結構有不相容的變更時，要調高 <see cref="CurrentSchemaVersion"/>
/// 並在 <see cref="SettingsMigrator"/> 加上舊版到新版的轉換。
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>每次儲存加一，用來偵測多位管理員同時修改。</summary>
    public long Revision { get; set; }

    public GatewaySettings Gateway { get; set; } = new();

    /// <summary>各 API 沒有自己設定時使用的連線。</summary>
    public DbConnectionSettings SharedConnection { get; set; } = new();

    /// <summary>以 server.json 的 id 為索引鍵。沒有列出的 API 使用 server.json 的預設值。</summary>
    public Dictionary<string, ServerSettings> Servers { get; set; } = [];
}

public sealed class GatewaySettings
{
    public int Port { get; set; } = 8080;
    public ListenScope Listen { get; set; } = ListenScope.Any;
}

public sealed class ServerSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>null 表示使用 server.json 的 defaultPort。</summary>
    public int? Port { get; set; }

    public ListenScope Listen { get; set; } = ListenScope.Localhost;

    /// <summary>此 API 自己的連線。沒有列在這裡的連線名稱使用共用預設連線。</summary>
    public Dictionary<string, DbConnectionSettings> Connections { get; set; } = [];
}

/// <summary>SQL Server 連線（SQL 驗證）。密碼以 DPAPI 加密後儲存。</summary>
public sealed class DbConnectionSettings
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? ProtectedPassword { get; set; }
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectTimeoutSeconds { get; set; } = 15;

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Server);
}
