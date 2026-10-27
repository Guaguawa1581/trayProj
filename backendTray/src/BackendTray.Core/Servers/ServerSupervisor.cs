using BackendTray.Contracts;
using BackendTray.Core.Platform;
using BackendTray.Core.Settings;
using Microsoft.Extensions.Logging;

namespace BackendTray.Core.Servers;

public enum RestartResult
{
    Restarted,
    NotFound,
    Paused,
}

/// <summary>
/// 管理所有子程序（Gateway 與各 API）：依設定啟動、暫停/繼續、套用設定變更，並定期做健康檢查。
/// </summary>
public sealed class ServerSupervisor : IAsyncDisposable
{
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(5);

    private readonly SettingsStore _settings;
    private readonly ServerCatalog _catalog;
    private readonly LaunchPlanner _planner;
    private readonly IServerLogWriterFactory _logs;
    private readonly ILogger<ServerSupervisor> _logger;
    private readonly JobObject _job = JobObject.CreateKillOnClose();
    private readonly HttpClient _healthClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyList<ManagedServer> _servers = [];
    private volatile IReadOnlyList<string> _warnings = [];
    private CancellationTokenSource? _healthLoop;

    public ServerSupervisor(
        SettingsStore settings,
        ServerCatalog catalog,
        LaunchPlanner planner,
        IServerLogWriterFactory logs,
        ILogger<ServerSupervisor> logger)
    {
        _settings = settings;
        _catalog = catalog;
        _planner = planner;
        _logs = logs;
        _logger = logger;
    }

    public bool IsPaused { get; private set; }

    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>目前由我們的子程序佔用的 port，驗證設定時不算衝突。</summary>
    public IReadOnlySet<int> ActivePorts => _servers.Where(s => s.HasProcess).Select(s => s.Spec.Port).ToHashSet();

    public IReadOnlyList<ServerStatusDto> GetStatuses() => _servers.Select(s => s.ToStatus()).ToList();

    public IReadOnlyList<string>? GetRecentLines(string serverId, int count) =>
        _servers.FirstOrDefault(s => s.Spec.Id == serverId)?.GetRecentLines(count);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IsPaused = false;
            await ApplyPlanLockedAsync(CreatePlan(_settings.Load()), start: true, cancellationToken);
            StartHealthLoop();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _healthLoop?.Cancel();
            _healthLoop = null;
            await StopServersAsync(_servers, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>停止所有子程序並釋放 port；服務本身與管理 API 繼續執行。</summary>
    public async Task PauseAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsPaused)
                return;
            IsPaused = true;
            _logger.LogInformation("暫停所有伺服器");
            await StopServersAsync(_servers, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsPaused)
                return;
            IsPaused = false;
            _logger.LogInformation("繼續所有伺服器");
            await ApplyPlanLockedAsync(CreatePlan(_settings.Load()), start: true, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>套用新設定，只重新啟動受影響的伺服器。回傳被重新啟動的伺服器名稱。</summary>
    public async Task<IReadOnlyList<string>> ApplySettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ApplyPlanLockedAsync(CreatePlan(settings), start: !IsPaused, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RestartResult> RestartAsync(string serverId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsPaused)
                return RestartResult.Paused;
            var server = _servers.FirstOrDefault(s => s.Spec.Id == serverId);
            if (server is null)
                return RestartResult.NotFound;

            await server.StopAsync(cancellationToken);
            server.Start();
            return RestartResult.Restarted;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
        {
            await StopAllAsync(timeout.Token);
        }

        foreach (var server in _servers)
            server.Dispose();
        _job.Dispose();
        _healthClient.Dispose();
        _gate.Dispose();
    }

    private LaunchPlan CreatePlan(AppSettings settings) => _planner.Create(settings, _catalog.Servers);

    private async Task<IReadOnlyList<string>> ApplyPlanLockedAsync(LaunchPlan plan, bool start, CancellationToken cancellationToken)
    {
        _warnings = plan.Warnings;
        foreach (var warning in plan.Warnings)
            _logger.LogWarning("{Warning}", warning);

        var specs = plan.StartOrder.ToList();
        var existing = _servers.ToDictionary(s => s.Spec.Id);

        // 已經不在清單中的伺服器（例如更新後移除的 API）。
        var removed = _servers.Where(s => specs.All(spec => spec.Id != s.Spec.Id)).ToList();
        await StopServersAsync(removed, cancellationToken);
        foreach (var server in removed)
            server.Dispose();

        // 需要停止、替換啟動資訊或啟動的伺服器，依啟動順序排列。
        var changes = new List<(ManagedServer Server, LaunchSpec? NewSpec)>();
        var ordered = new List<ManagedServer>();
        foreach (var spec in specs)
        {
            if (existing.TryGetValue(spec.Id, out var server))
            {
                if (server.Spec.Fingerprint != spec.Fingerprint)
                    changes.Add((server, spec));
                else if (!server.ShouldRun)
                    changes.Add((server, null));
            }
            else
            {
                server = new ManagedServer(spec, _job, _logs.Create(spec.Id), _logger);
                changes.Add((server, null));
            }

            ordered.Add(server);
        }

        // 顯示時 Gateway 排第一。
        _servers = ordered.OrderBy(s => s.Spec.Kind == ServerKind.Gateway ? 0 : 1).ToList();

        var restarted = changes
            .Where(change => change.NewSpec is not null && change.Server.ShouldRun)
            .Select(change => change.Server.Spec.DisplayName)
            .ToList();

        await StopServersAsync(changes.Select(change => change.Server), cancellationToken);
        foreach (var (server, newSpec) in changes)
        {
            if (newSpec is not null)
                server.ReplaceSpec(newSpec);
        }

        if (start)
        {
            foreach (var (server, _) in changes)
                server.Start();
        }

        return restarted;
    }

    private static async Task StopServersAsync(IEnumerable<ManagedServer> servers, CancellationToken cancellationToken)
    {
        // 先停 Gateway（不再接新請求），再同時停止各 API。
        var list = servers.ToList();
        foreach (var gateway in list.Where(s => s.Spec.Kind == ServerKind.Gateway))
            await gateway.StopAsync(cancellationToken);
        await Task.WhenAll(list.Where(s => s.Spec.Kind != ServerKind.Gateway).Select(s => s.StopAsync(cancellationToken)));
    }

    private void StartHealthLoop()
    {
        if (_healthLoop is not null)
            return;
        _healthLoop = new CancellationTokenSource();
        _ = HealthLoopAsync(_healthLoop.Token);
    }

    private async Task HealthLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(HealthCheckInterval);
        try
        {
            do
            {
                var running = _servers.Where(s => s.HasProcess).ToList();
                await Task.WhenAll(running.Select(async server =>
                    server.ReportHealth(await ProbeAsync(server.Spec.HealthUrl, cancellationToken))));
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康檢查迴圈發生錯誤");
        }
    }

    private async Task<bool> ProbeAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _healthClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return false;
        }
    }
}
