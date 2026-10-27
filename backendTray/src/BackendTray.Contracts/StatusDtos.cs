namespace BackendTray.Contracts;

public sealed class ServiceStatusDto
{
    public string Version { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public bool Paused { get; set; }
    public string InstallRoot { get; set; } = "";
    public string DataRoot { get; set; } = "";
    public string LogsDirectory { get; set; } = "";
    public List<ServerStatusDto> Servers { get; set; } = [];

    /// <summary>不影響執行、但管理員應該知道的事，例如資料庫連線尚未設定。</summary>
    public List<string> Warnings { get; set; } = [];
}

public sealed class ServerStatusDto
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public ServerKind Kind { get; set; }
    public ServerState State { get; set; }
    public int Port { get; set; }
    public ListenScope Listen { get; set; }
    public int? ProcessId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public int RestartCount { get; set; }
    public string? Message { get; set; }
}

public sealed class ServerLogDto
{
    public string ServerId { get; set; } = "";
    public List<string> Lines { get; set; } = [];
}
