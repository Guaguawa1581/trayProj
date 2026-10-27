using Microsoft.AspNetCore.Diagnostics;

namespace MainApi.Data;

/// <summary>還沒設定資料庫連線時回傳 503 與原因，而不是沒有說明的 500。其他例外交給預設處理（500，並寫入 log）。</summary>
public sealed class DatabaseExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DatabaseNotConfiguredException)
            return false;

        await Results.Problem(
                title: "資料庫無法使用",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable)
            .ExecuteAsync(httpContext);
        return true;
    }
}
