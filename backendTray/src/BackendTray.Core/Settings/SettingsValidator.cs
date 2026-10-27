using BackendTray.Core.Servers;

namespace BackendTray.Core.Settings;

public static class SettingsValidator
{
    /// <param name="portsOwnedByUs">目前由我們的子程序佔用的 port，不算衝突。</param>
    /// <param name="isPortInUse">檢查 port 是否被任何程式監聽。</param>
    public static List<string> Validate(
        AppSettings proposed,
        AppSettings current,
        IReadOnlyList<ServerManifest> manifests,
        IReadOnlySet<int> portsOwnedByUs,
        Func<int, bool> isPortInUse)
    {
        var errors = new List<string>();
        var ports = new List<(string Owner, int Port, int CurrentPort)>
        {
            ("前端 Gateway", proposed.Gateway.Port, current.Gateway.Port),
        };

        ValidateConnection("共用預設連線", proposed.SharedConnection, required: false, errors);

        foreach (var manifest in manifests)
        {
            var server = proposed.Servers.GetValueOrDefault(manifest.Id) ?? new ServerSettings();
            var currentPort = current.Servers.GetValueOrDefault(manifest.Id)?.Port ?? manifest.DefaultPort;
            ports.Add((manifest.DisplayName, server.Port ?? manifest.DefaultPort, currentPort));

            if (!server.Enabled)
                continue;
            foreach (var (name, connection) in server.Connections)
            {
                if (manifest.Connections.Contains(name))
                    ValidateConnection($"{manifest.DisplayName}／{name}", connection, required: true, errors);
            }
        }

        foreach (var (owner, port, _) in ports.Where(p => p.Port is < 1 or > 65535))
            errors.Add($"{owner} 的連接埠 {port} 無效，必須在 1–65535 之間。");

        foreach (var group in ports.GroupBy(p => p.Port).Where(g => g.Count() > 1))
            errors.Add($"連接埠 {group.Key} 重複：{string.Join("、", group.Select(p => p.Owner))}。");

        foreach (var (owner, port, currentPort) in ports.Where(p => p.Port != p.CurrentPort && p.Port is >= 1 and <= 65535))
        {
            if (!portsOwnedByUs.Contains(port) && isPortInUse(port))
                errors.Add($"連接埠 {port}（{owner}）已被其他程式使用。");
        }

        return errors;
    }

    private static void ValidateConnection(string label, DbConnectionSettings connection, bool required, List<string> errors)
    {
        if (!required && !connection.IsConfigured)
            return;

        if (string.IsNullOrWhiteSpace(connection.Server))
            errors.Add($"{label}：請輸入 SQL Server 位址。");
        if (string.IsNullOrWhiteSpace(connection.Database))
            errors.Add($"{label}：請輸入資料庫名稱。");
        if (string.IsNullOrWhiteSpace(connection.UserId))
            errors.Add($"{label}：請輸入帳號。");
        if (connection.ProtectedPassword is null)
            errors.Add($"{label}：請輸入密碼。");
        if (connection.ConnectTimeoutSeconds is < 1 or > 300)
            errors.Add($"{label}：逾時秒數必須在 1–300 之間。");
    }
}
