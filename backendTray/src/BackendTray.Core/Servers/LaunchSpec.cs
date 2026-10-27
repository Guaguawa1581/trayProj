using BackendTray.Contracts;

namespace BackendTray.Core.Servers;

/// <summary>啟動一個子程序所需的全部資訊。內容不同就代表需要重新啟動。</summary>
public sealed class LaunchSpec
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required ServerKind Kind { get; init; }
    public required string ExecutablePath { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
    public required int Port { get; init; }
    public required ListenScope Listen { get; init; }
    public required string HealthUrl { get; init; }
    public bool Enabled { get; init; } = true;

    /// <summary>啟動前要寫入的設定檔，例如 Gateway 的路由設定。</summary>
    public string? ConfigFilePath { get; init; }
    public string? ConfigFileContent { get; init; }

    public string Fingerprint => string.Join('\n',
    [
        ExecutablePath,
        string.Join('\u001f', Arguments),
        .. Environment.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"),
        Enabled.ToString(),
        ConfigFileContent ?? "",
    ]);
}

public sealed record LaunchPlan(LaunchSpec Gateway, IReadOnlyList<LaunchSpec> Apis, IReadOnlyList<string> Warnings)
{
    /// <summary>先啟動 API，最後啟動 Gateway；停止時順序相反。</summary>
    public IEnumerable<LaunchSpec> StartOrder => Apis.Append(Gateway);
}
