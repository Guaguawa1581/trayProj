namespace BackendTray.Contracts;

/// <summary>管理介面看到的設定。密碼只寫不讀：讀取時只會知道「有沒有設定」。</summary>
public sealed class SettingsDto
{
    /// <summary>儲存時必須帶回讀取時的值；不一致代表有其他管理員先儲存過。</summary>
    public long Revision { get; set; }

    public GatewaySettingsDto Gateway { get; set; } = new();

    /// <summary>各 API 沒有自己設定時使用的連線。</summary>
    public DbConnectionDto SharedConnection { get; set; } = new();

    public List<ServerSettingsDto> Servers { get; set; } = [];
}

public sealed class GatewaySettingsDto
{
    public int Port { get; set; }
    public ListenScope Listen { get; set; }
}

public sealed class ServerSettingsDto
{
    public string Id { get; set; } = "";

    /// <summary>唯讀，來自 server.json。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>唯讀，來自 server.json。</summary>
    public int DefaultPort { get; set; }

    public bool Enabled { get; set; } = true;
    public int Port { get; set; }
    public ListenScope Listen { get; set; }

    /// <summary>此 API 在 server.json 宣告需要的連線（例如 Default、Report）。</summary>
    public List<ServerConnectionDto> Connections { get; set; } = [];
}

public sealed class ServerConnectionDto
{
    public string Name { get; set; } = "";

    /// <summary>true 時使用共用預設連線，<see cref="Connection"/> 會被忽略。</summary>
    public bool UseShared { get; set; } = true;

    public DbConnectionDto? Connection { get; set; }
}

public sealed class DbConnectionDto
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public string UserId { get; set; } = "";

    /// <summary>唯讀：服務端已儲存密碼。</summary>
    public bool HasPassword { get; set; }

    /// <summary>只寫：有值時取代已儲存的密碼；空白表示不變更。</summary>
    public string? NewPassword { get; set; }

    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectTimeoutSeconds { get; set; } = 15;
}

public sealed class SaveSettingsResult
{
    public SaveOutcome Outcome { get; set; }
    public long Revision { get; set; }
    public List<string> Errors { get; set; } = [];

    /// <summary>因設定變更而重新啟動的伺服器顯示名稱。</summary>
    public List<string> RestartedServers { get; set; } = [];
}

/// <summary>連線設定在設定檔中的位置，用來在「密碼留空」時找出已儲存的密碼。</summary>
public static class ConnectionSlots
{
    public const string Shared = "shared";

    public static string ForServer(string serverId, string connectionName) => $"{serverId}:{connectionName}";
}

public sealed class DbTestRequest
{
    public DbConnectionDto Connection { get; set; } = new();

    /// <summary>密碼留空時，改用這個位置已儲存的密碼（見 <see cref="ConnectionSlots"/>）。</summary>
    public string? StoredPasswordSlot { get; set; }
}

public sealed class DbTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Detail { get; set; }
    public string? ServerVersion { get; set; }
    public long ElapsedMilliseconds { get; set; }
}
