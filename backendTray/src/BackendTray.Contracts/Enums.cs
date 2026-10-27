namespace BackendTray.Contracts;

/// <summary>伺服器接受連線的範圍。</summary>
public enum ListenScope
{
    /// <summary>只接受本機連線（localhost）。</summary>
    Localhost,

    /// <summary>接受所有網路介面的連線。</summary>
    Any,
}

public enum ServerKind
{
    Gateway,
    Api,
}

public enum ServerState
{
    Disabled,
    Stopped,
    Starting,
    Running,
    Unhealthy,
    Stopping,
    Faulted,
}

public enum SaveOutcome
{
    Saved,
    Invalid,
    Conflict,
}
