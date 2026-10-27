using Microsoft.Data.SqlClient;

namespace MainApi.Data;

/// <summary>
/// 依 ConnectionStrings:Default 建立連線。由 BackendTray 啟動時，連線字串來自管理介面的「資料庫」設定；
/// 單獨 dotnet run 時請用 user-secrets 設定。
/// </summary>
public sealed class SqlConnectionFactory(IConfiguration configuration)
{
    public const string ConnectionName = "Default";

    /// <summary>回傳尚未開啟的連線，由 Dapper 在執行時開啟與關閉。</summary>
    public SqlConnection Create()
    {
        var connectionString = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new DatabaseNotConfiguredException(ConnectionName);

        return new SqlConnection(connectionString);
    }
}

public sealed class DatabaseNotConfiguredException(string connectionName)
    : InvalidOperationException($"尚未設定資料庫連線「{connectionName}」：請在管理介面的「資料庫」頁設定，或單獨執行時用 user-secrets 設定 ConnectionStrings:{connectionName}。");
