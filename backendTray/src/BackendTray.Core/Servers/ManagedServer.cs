using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using BackendTray.Contracts;
using BackendTray.Core.Platform;
using Microsoft.Extensions.Logging;

namespace BackendTray.Core.Servers;

/// <summary>
/// 管理一個子程序：啟動、優雅停止、意外結束時自動重啟，並保留最近的輸出供管理介面查看。
/// </summary>
public sealed class ManagedServer : IDisposable
{
    private const int MaxRecentLines = 1000;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StableRunTime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxRestartDelay = TimeSpan.FromSeconds(60);

    private readonly JobObject _job;
    private readonly IServerLogWriter _log;
    private readonly ILogger _logger;
    private readonly Lock _sync = new();
    private readonly ConcurrentQueue<string> _recentLines = new();
    private Process? _process;
    private CancellationTokenSource? _pendingRestart;
    private bool _shouldRun;
    private int _consecutiveFailures;

    public ManagedServer(LaunchSpec spec, JobObject job, IServerLogWriter log, ILogger logger)
    {
        Spec = spec;
        _job = job;
        _log = log;
        _logger = logger;
        State = spec.Enabled ? ServerState.Stopped : ServerState.Disabled;
    }

    public LaunchSpec Spec { get; private set; }
    public ServerState State { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public int RestartCount { get; private set; }
    public string? Message { get; private set; }

    /// <summary>目前是否應該在執行（已啟動且尚未要求停止）。</summary>
    public bool ShouldRun
    {
        get { lock (_sync) return _shouldRun; }
    }

    public bool HasProcess
    {
        get { lock (_sync) return _process is not null; }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (!Spec.Enabled)
            {
                State = ServerState.Disabled;
                Message = null;
                return;
            }

            _shouldRun = true;
            _consecutiveFailures = 0;
            LaunchLocked();
        }
    }

    /// <summary>只能在停止狀態下替換啟動資訊。</summary>
    public void ReplaceSpec(LaunchSpec spec)
    {
        lock (_sync)
        {
            if (_process is not null || _shouldRun)
                throw new InvalidOperationException("伺服器執行中，不能替換啟動資訊。");
            Spec = spec;
            State = spec.Enabled ? ServerState.Stopped : ServerState.Disabled;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Process? process;
        lock (_sync)
        {
            _shouldRun = false;
            _pendingRestart?.Cancel();
            _pendingRestart = null;
            process = _process;
            if (process is null)
            {
                State = Spec.Enabled ? ServerState.Stopped : ServerState.Disabled;
                StartedAt = null;
                return;
            }

            State = ServerState.Stopping;
        }

        WriteSystemLine("正在停止…");
        try
        {
            // BackendTray.Agent 收到 shutdown（或 stdin 被關閉）時會優雅結束。
            process.StandardInput.WriteLine("shutdown");
            process.StandardInput.Close();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // 程序已經結束或沒有讀 stdin。
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StopTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            WriteSystemLine("未在時限內結束，強制終止。");
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                _logger.LogWarning(ex, "{Server}: 強制終止失敗", Spec.Id);
            }
        }

        lock (_sync)
        {
            if (ReferenceEquals(_process, process))
                _process = null;
            State = Spec.Enabled ? ServerState.Stopped : ServerState.Disabled;
            StartedAt = null;
            Message = null;
        }

        WriteSystemLine("已停止。");
        process.Dispose();
    }

    /// <summary>由 Supervisor 定期呼叫，回報健康檢查結果。</summary>
    public void ReportHealth(bool healthy)
    {
        lock (_sync)
        {
            if (_process is null || State is not (ServerState.Starting or ServerState.Running or ServerState.Unhealthy))
                return;

            if (healthy)
            {
                State = ServerState.Running;
                Message = null;
            }
            else if (DateTimeOffset.Now - StartedAt < StartupGracePeriod)
            {
                State = ServerState.Starting;
            }
            else
            {
                State = ServerState.Unhealthy;
                Message = $"健康檢查失敗（{Spec.HealthUrl}）";
            }
        }
    }

    public ServerStatusDto ToStatus()
    {
        lock (_sync)
        {
            return new ServerStatusDto
            {
                Id = Spec.Id,
                DisplayName = Spec.DisplayName,
                Kind = Spec.Kind,
                State = State,
                Port = Spec.Port,
                Listen = Spec.Listen,
                ProcessId = _process?.Id,
                StartedAt = StartedAt,
                RestartCount = RestartCount,
                Message = Message,
            };
        }
    }

    public IReadOnlyList<string> GetRecentLines(int count) =>
        _recentLines.ToArray().TakeLast(Math.Clamp(count, 1, MaxRecentLines)).ToList();

    public void Dispose()
    {
        lock (_sync)
        {
            _shouldRun = false;
            _pendingRestart?.Cancel();
        }

        _log.Dispose();
    }

    private void LaunchLocked()
    {
        if (_process is not null)
            return;

        if (!File.Exists(Spec.ExecutablePath))
        {
            // 檔案不會自己出現，不排程重試。
            State = ServerState.Faulted;
            Message = $"找不到執行檔：{Spec.ExecutablePath}";
            WriteSystemLine(Message);
            return;
        }

        try
        {
            if (Spec.ConfigFilePath is not null)
                AtomicFile.WriteAllText(Spec.ConfigFilePath, Spec.ConfigFileContent ?? "");

            var process = new Process { StartInfo = CreateStartInfo(), EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => OnOutput(e.Data);
            process.ErrorDataReceived += (_, e) => OnOutput(e.Data);
            process.Exited += (_, _) => OnExited(process);
            process.Start();
            _job.Assign(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _process = process;
            StartedAt = DateTimeOffset.Now;
            State = ServerState.Starting;
            Message = null;
            WriteSystemLine($"已啟動（PID {process.Id}，port {Spec.Port}）。");
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            State = ServerState.Faulted;
            Message = $"無法啟動：{ex.Message}";
            WriteSystemLine(Message);
            ScheduleRestartLocked();
        }
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo(Spec.ExecutablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(Spec.ExecutablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in Spec.Arguments)
            startInfo.ArgumentList.Add(argument);
        foreach (var (key, value) in Spec.Environment)
            startInfo.Environment[key] = value;
        return startInfo;
    }

    private void OnExited(Process process)
    {
        try
        {
            // 等非同步輸出讀完，避免最後幾行 log 遺失。
            process.WaitForExit();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // StopAsync 已經收尾並釋放了這個程序。
        }

        lock (_sync)
        {
            // 正在停止時由 StopAsync 收尾。
            if (!ReferenceEquals(_process, process) || !_shouldRun)
                return;

            _process = null;
            var exitCode = process.ExitCode;
            var ranFor = DateTimeOffset.Now - (StartedAt ?? DateTimeOffset.Now);
            process.Dispose();

            if (ranFor > StableRunTime)
                _consecutiveFailures = 0;
            _consecutiveFailures++;

            State = ServerState.Faulted;
            StartedAt = null;
            Message = $"程序意外結束（代碼 {exitCode}）";
            WriteSystemLine(Message);
            ScheduleRestartLocked();
        }
    }

    private void ScheduleRestartLocked()
    {
        if (!_shouldRun)
            return;

        var seconds = Math.Min(MaxRestartDelay.TotalSeconds, Math.Pow(2, Math.Max(0, _consecutiveFailures - 1)));
        var delay = TimeSpan.FromSeconds(seconds);
        Message = $"{Message}，{seconds:0} 秒後重新啟動";

        _pendingRestart?.Cancel();
        var pending = new CancellationTokenSource();
        _pendingRestart = pending;

        _ = Task.Delay(delay, pending.Token).ContinueWith(task =>
        {
            if (task.IsCanceled)
                return;

            lock (_sync)
            {
                if (!_shouldRun || _process is not null || !ReferenceEquals(_pendingRestart, pending))
                    return;
                _pendingRestart = null;
                RestartCount++;
                LaunchLocked();
            }
        }, TaskScheduler.Default);
    }

    private void OnOutput(string? line)
    {
        if (line is null)
            return;
        _log.Write(line);
        AddRecentLine(line);
    }

    private void WriteSystemLine(string text)
    {
        var line = $"[BackendTray] {text}";
        _log.Write(line);
        AddRecentLine(line);
        _logger.LogInformation("{Server}: {Text}", Spec.Id, text);
    }

    private void AddRecentLine(string line)
    {
        _recentLines.Enqueue($"{DateTime.Now:HH:mm:ss} {line}");
        while (_recentLines.Count > MaxRecentLines)
            _recentLines.TryDequeue(out _);
    }
}
