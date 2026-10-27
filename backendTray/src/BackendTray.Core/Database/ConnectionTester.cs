using System.Diagnostics;
using System.Security.Authentication;
using BackendTray.Contracts;
using BackendTray.Core.Settings;
using Microsoft.Data.SqlClient;

namespace BackendTray.Core.Database;

/// <summary>
/// 測試 SQL Server 連線。由服務執行，所以結果反映的是服務實際連線時的網路環境。
/// </summary>
public sealed class ConnectionTester(ConnectionStringFactory connectionStrings)
{
    private const string ApplicationName = "BackendTray (連線測試)";

    public async Task<DbTestResult> TestAsync(DbConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(settings.Server)) return Fail("請輸入 SQL Server 位址。");
        if (string.IsNullOrWhiteSpace(settings.Database)) return Fail("請輸入資料庫名稱。");
        if (string.IsNullOrWhiteSpace(settings.UserId)) return Fail("請輸入帳號。");

        try
        {
            await using var connection = new SqlConnection(connectionStrings.Build(settings, ApplicationName, password));
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT @@VERSION", connection);
            var version = FirstLine(await command.ExecuteScalarAsync(cancellationToken) as string);
            return new DbTestResult
            {
                Success = true,
                Message = $"連線成功（{stopwatch.ElapsedMilliseconds} ms）。",
                ServerVersion = version,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            };
        }
        catch (SqlException ex) when (ex.Number == 4060)
        {
            // 能登入但開不了資料庫：改連 master 分辨「不存在」與「沒有權限」。
            return await DiagnoseDatabaseAsync(settings, password, ex, stopwatch, cancellationToken);
        }
        catch (SqlException ex)
        {
            return Fail(Describe(ex), ex.Message, stopwatch);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Fail("連線設定無效。", ex.Message, stopwatch);
        }
    }

    private async Task<DbTestResult> DiagnoseDatabaseAsync(
        DbConnectionSettings settings, string password, SqlException original, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionStrings.Build(settings, ApplicationName, password, database: "master"));
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT DB_ID(@name)", connection);
            command.Parameters.AddWithValue("@name", settings.Database.Trim());
            var exists = await command.ExecuteScalarAsync(cancellationToken) is not (null or DBNull);
            return Fail(
                exists
                    ? $"已登入 SQL Server，但帳號「{settings.UserId}」沒有資料庫「{settings.Database}」的存取權限。"
                    : $"已登入 SQL Server，但找不到資料庫「{settings.Database}」。",
                original.Message,
                stopwatch);
        }
        catch (SqlException)
        {
            return Fail(Describe(original), original.Message, stopwatch);
        }
    }

    private static string Describe(SqlException ex)
    {
        if (IsCertificateError(ex))
            return "SQL Server 的憑證不受信任：可勾選「信任伺服器憑證」，或在 SQL Server 安裝受信任的憑證。";

        return ex.Number switch
        {
            18456 => "登入失敗：帳號或密碼錯誤，或 SQL Server 未啟用「SQL Server 及 Windows 驗證模式」。",
            18487 or 18488 => "登入失敗：密碼已過期或必須變更。",
            4060 => "無法開啟資料庫：資料庫不存在，或帳號沒有存取權限。",
            -2 => "連線逾時：請確認伺服器位址與網路狀況，或調高逾時秒數。",
            -1 or 2 or 26 or 40 or 53 or 258 or 1225 or 10060 or 10061 or 11001 =>
                "連不到 SQL Server：請確認伺服器位址、執行個體名稱、TCP/IP 通訊協定已啟用，以及防火牆設定。",
            _ => $"連線失敗（SQL 錯誤 {ex.Number}）。",
        };
    }

    private static bool IsCertificateError(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is AuthenticationException) return true;
        }

        return ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstLine(string? text) =>
        text?.Split('\n', 2)[0].Trim();

    private static DbTestResult Fail(string message, string? detail = null, Stopwatch? stopwatch = null) => new()
    {
        Success = false,
        Message = message,
        Detail = detail,
        ElapsedMilliseconds = stopwatch?.ElapsedMilliseconds ?? 0,
    };
}
