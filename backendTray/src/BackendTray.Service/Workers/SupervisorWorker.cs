using BackendTray.Core.Servers;

namespace BackendTray.Service.Workers;

/// <summary>服務啟動時啟動所有伺服器，服務停止時優雅地停止它們。</summary>
internal sealed class SupervisorWorker(ServerSupervisor supervisor, ILogger<SupervisorWorker> logger) : IHostedService
{
    private Task? _startup;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 不阻塞服務啟動：SCM 要求服務在時限內回報「執行中」，管理 API 也能先開始回應。
        _startup = Task.Run(async () =>
        {
            try
            {
                await supervisor.StartAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "啟動伺服器時發生錯誤");
            }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_startup is not null)
            await _startup.WaitAsync(cancellationToken);
        await supervisor.StopAllAsync(cancellationToken);
    }
}
