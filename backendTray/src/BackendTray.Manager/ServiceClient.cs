using System.IO.Pipes;
using System.Net;
using System.Net.Http.Json;
using BackendTray.Contracts;

namespace BackendTray.Manager;

public enum ServiceClientError
{
    Unreachable,
    AccessDenied,
    Failed,
}

public sealed class ServiceClientException(ServiceClientError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ServiceClientError Error { get; } = error;
}

/// <summary>透過 named pipe 呼叫服務的管理 API。</summary>
public sealed class ServiceClient : IDisposable
{
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(90);

    private readonly HttpClient _http;

    public ServiceClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var pipe = new NamedPipeClientStream(".", ManagementConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(1000, cancellationToken);
                    return pipe;
                }
                catch
                {
                    await pipe.DisposeAsync();
                    throw;
                }
            },
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri("http://backendtray/"), Timeout = Timeout.InfiniteTimeSpan };
    }

    public Task<ServiceStatusDto> GetStatusAsync() =>
        SendAsync<ServiceStatusDto>(HttpMethod.Get, "status", null, StatusTimeout);

    public Task PauseAsync() => SendAsync<object>(HttpMethod.Post, "servers/pause", null, OperationTimeout);

    public Task ResumeAsync() => SendAsync<object>(HttpMethod.Post, "servers/resume", null, OperationTimeout);

    public Task RestartServerAsync(string serverId) =>
        SendAsync<object>(HttpMethod.Post, $"servers/{Uri.EscapeDataString(serverId)}/restart", null, OperationTimeout);

    public Task<ServerLogDto> GetLogAsync(string serverId, int tail) =>
        SendAsync<ServerLogDto>(HttpMethod.Get, $"logs/{Uri.EscapeDataString(serverId)}?tail={tail}", null, StatusTimeout);

    public Task<SettingsDto> GetSettingsAsync() =>
        SendAsync<SettingsDto>(HttpMethod.Get, "settings", null, TimeSpan.FromSeconds(10));

    /// <summary>驗證失敗與版本衝突也會回傳結果（不丟例外），由呼叫端依 <see cref="SaveSettingsResult.Outcome"/> 處理。</summary>
    public Task<SaveSettingsResult> SaveSettingsAsync(SettingsDto settings) =>
        SendAsync<SaveSettingsResult>(HttpMethod.Put, "settings", settings, OperationTimeout, HttpStatusCode.BadRequest, HttpStatusCode.Conflict);

    public Task<DbTestResult> TestDatabaseAsync(DbTestRequest request) =>
        SendAsync<DbTestResult>(HttpMethod.Post, "database/test", request, TimeSpan.FromMinutes(5));

    public void Dispose() => _http.Dispose();

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, TimeSpan timeout, params HttpStatusCode[] acceptedErrors)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: ManagementJson.Options);

            using var response = await _http.SendAsync(request, cancellation.Token);
            if (!response.IsSuccessStatusCode && !acceptedErrors.Contains(response.StatusCode))
            {
                var text = await response.Content.ReadAsStringAsync(cancellation.Token);
                throw new ServiceClientException(ServiceClientError.Failed,
                    string.IsNullOrWhiteSpace(text) ? $"服務回應錯誤（HTTP {(int)response.StatusCode}）。" : text.Trim('"'));
            }

            if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
                return default!;
            return await response.Content.ReadFromJsonAsync<T>(ManagementJson.Options, cancellation.Token)
                ?? throw new ServiceClientException(ServiceClientError.Failed, "服務回應的內容是空的。");
        }
        catch (HttpRequestException ex) when (FindInner<UnauthorizedAccessException>(ex) is not null)
        {
            throw new ServiceClientException(ServiceClientError.AccessDenied, "權限不足，無法連線到服務。請以系統管理員身分執行管理介面。", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ServiceClientException(ServiceClientError.Unreachable, "無法連線到 BackendTray 服務。", ex);
        }
        catch (OperationCanceledException ex) when (cancellation.IsCancellationRequested)
        {
            throw new ServiceClientException(ServiceClientError.Unreachable, "服務沒有在時限內回應。", ex);
        }
    }

    private static T? FindInner<T>(Exception exception) where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match)
                return match;
        }

        return null;
    }
}
