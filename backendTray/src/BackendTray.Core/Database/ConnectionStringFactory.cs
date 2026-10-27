using BackendTray.Core.Settings;
using Microsoft.Data.SqlClient;

namespace BackendTray.Core.Database;

public sealed class ConnectionStringFactory(SecretProtector protector)
{
    /// <param name="plainPassword">有值時使用此密碼，否則解密設定中儲存的密碼。</param>
    /// <param name="database">有值時覆寫設定中的資料庫（例如連線測試時改連 master）。</param>
    public string Build(DbConnectionSettings settings, string applicationName, string? plainPassword = null, string? database = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(),
            InitialCatalog = database ?? settings.Database.Trim(),
            UserID = settings.UserId.Trim(),
            Password = plainPassword ?? DecryptPassword(settings),
            Encrypt = settings.Encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = settings.TrustServerCertificate,
            ConnectTimeout = settings.ConnectTimeoutSeconds,
            ApplicationName = applicationName,
        };
        return builder.ConnectionString;
    }

    private string DecryptPassword(DbConnectionSettings settings) =>
        settings.ProtectedPassword is null ? "" : protector.Unprotect(settings.ProtectedPassword);
}
