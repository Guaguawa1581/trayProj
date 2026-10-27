using System.Security.Cryptography;
using BackendTray.Contracts;
using BackendTray.Core.Servers;

namespace BackendTray.Core.Settings;

/// <summary>設定檔與管理 API DTO 之間的轉換。密碼只在這裡加密，不會以明文回傳給管理介面。</summary>
public sealed class SettingsMapper(ServerCatalog catalog, SecretProtector protector)
{
    public SettingsDto ToDto(AppSettings settings) => new()
    {
        Revision = settings.Revision,
        Gateway = new GatewaySettingsDto { Port = settings.Gateway.Port, Listen = settings.Gateway.Listen },
        SharedConnection = ToDto(settings.SharedConnection),
        Servers = catalog.Servers.Select(manifest =>
        {
            var server = settings.Servers.GetValueOrDefault(manifest.Id) ?? new ServerSettings();
            return new ServerSettingsDto
            {
                Id = manifest.Id,
                DisplayName = manifest.DisplayName,
                DefaultPort = manifest.DefaultPort,
                Enabled = server.Enabled,
                Port = server.Port ?? manifest.DefaultPort,
                Listen = server.Listen,
                Connections = manifest.Connections.Select(name => server.Connections.TryGetValue(name, out var own)
                    ? new ServerConnectionDto { Name = name, UseShared = false, Connection = ToDto(own) }
                    : new ServerConnectionDto { Name = name, UseShared = true }).ToList(),
            };
        }).ToList(),
    };

    /// <summary>
    /// 以目前設定為基礎套用管理介面送來的變更。密碼留空時沿用已儲存的密碼；
    /// 目前沒有安裝的 API（例如暫時移除的）其設定保留不動。
    /// </summary>
    public AppSettings Apply(SettingsDto dto, AppSettings current)
    {
        var result = SettingsJson.Clone(current);
        result.Gateway = new GatewaySettings { Port = dto.Gateway.Port, Listen = dto.Gateway.Listen };
        result.SharedConnection = FromDto(dto.SharedConnection, current.SharedConnection.ProtectedPassword);

        foreach (var serverDto in dto.Servers)
        {
            if (catalog.Find(serverDto.Id) is not { } manifest)
                continue;

            var previous = current.Servers.GetValueOrDefault(manifest.Id);
            var server = new ServerSettings { Enabled = serverDto.Enabled, Port = serverDto.Port, Listen = serverDto.Listen };
            foreach (var connection in serverDto.Connections)
            {
                if (connection.UseShared || connection.Connection is null || !manifest.Connections.Contains(connection.Name))
                    continue;
                var previousPassword = previous?.Connections.GetValueOrDefault(connection.Name)?.ProtectedPassword;
                server.Connections[connection.Name] = FromDto(connection.Connection, previousPassword);
            }

            result.Servers[manifest.Id] = server;
        }

        return result;
    }

    public DbConnectionSettings FromDto(DbConnectionDto dto, string? storedProtectedPassword) => new()
    {
        Server = dto.Server.Trim(),
        Database = dto.Database.Trim(),
        UserId = dto.UserId.Trim(),
        ProtectedPassword = string.IsNullOrEmpty(dto.NewPassword) ? storedProtectedPassword : protector.Protect(dto.NewPassword),
        Encrypt = dto.Encrypt,
        TrustServerCertificate = dto.TrustServerCertificate,
        ConnectTimeoutSeconds = dto.ConnectTimeoutSeconds,
    };

    /// <summary>取得 <paramref name="slot"/>（見 <see cref="ConnectionSlots"/>）已儲存的明文密碼；沒有或無法解密時回傳 null。</summary>
    public string? GetStoredPassword(string? slot, AppSettings settings)
    {
        string? protectedPassword = null;
        if (slot == ConnectionSlots.Shared)
        {
            protectedPassword = settings.SharedConnection.ProtectedPassword;
        }
        else if (slot?.Split(':', 2) is [var serverId, var name])
        {
            protectedPassword = settings.Servers.GetValueOrDefault(serverId)?.Connections.GetValueOrDefault(name)?.ProtectedPassword;
        }

        if (protectedPassword is null)
            return null;

        try
        {
            return protector.Unprotect(protectedPassword);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static DbConnectionDto ToDto(DbConnectionSettings settings) => new()
    {
        Server = settings.Server,
        Database = settings.Database,
        UserId = settings.UserId,
        HasPassword = settings.ProtectedPassword is not null,
        Encrypt = settings.Encrypt,
        TrustServerCertificate = settings.TrustServerCertificate,
        ConnectTimeoutSeconds = settings.ConnectTimeoutSeconds,
    };
}
