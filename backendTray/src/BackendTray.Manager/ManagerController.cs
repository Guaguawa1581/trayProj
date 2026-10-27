using BackendTray.Manager.Platform;

namespace BackendTray.Manager;

internal enum PrimaryAction
{
    None,
    Pause,
    Resume,
    StartService,
}

/// <summary>
/// 管理介面的核心：每 2 秒向服務查詢狀態，並提供系統匣選單與主畫面共用的操作。
/// 所有事件都在 UI 執行緒上觸發。
/// </summary>
internal sealed class ManagerController : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private bool _refreshing;

    public ManagerController(ServiceClient client)
    {
        Client = client;
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public ServiceClient Client { get; }

    public ManagerStatus Status { get; private set; } = ManagerStatus.Initial;

    public bool Busy { get; private set; }

    public event EventHandler? StatusChanged;

    public event EventHandler? BusyChanged;

    public PrimaryAction PrimaryAction => Status.State switch
    {
        TrayState.Paused => PrimaryAction.Resume,
        TrayState.Stopped => PrimaryAction.StartService,
        TrayState.Running or TrayState.Starting or TrayState.Warning or TrayState.Error => PrimaryAction.Pause,
        _ => PrimaryAction.None,
    };

    public void Start()
    {
        _timer.Start();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_refreshing)
            return;
        _refreshing = true;
        try
        {
            var scmStatus = await Task.Run(ServiceControl.QueryStatus);
            try
            {
                Status = ManagerStatus.FromService(scmStatus, await Client.GetStatusAsync());
            }
            catch (ServiceClientException ex)
            {
                Status = ManagerStatus.FromFailure(scmStatus, ex);
            }

            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _refreshing = false;
        }
    }

    public Task<bool> RunPrimaryActionAsync(IWin32Window? owner) => PrimaryAction switch
    {
        PrimaryAction.Pause => RunAsync("暫停服務", Client.PauseAsync, owner),
        PrimaryAction.Resume => RunAsync("繼續服務", Client.ResumeAsync, owner),
        PrimaryAction.StartService => RunAsync("啟動服務", ServiceControl.StartAsync, owner),
        _ => Task.FromResult(false),
    };

    /// <summary>執行一個操作：期間標記為忙碌，失敗時顯示錯誤，結束後立即更新狀態。</summary>
    public async Task<bool> RunAsync(string title, Func<Task> action, IWin32Window? owner)
    {
        if (Busy)
            return false;

        SetBusy(true);
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            Ui.ShowError(owner, $"{title}失敗", ex);
            return false;
        }
        finally
        {
            SetBusy(false);
            await RefreshAsync();
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        Client.Dispose();
    }

    private void SetBusy(bool busy)
    {
        Busy = busy;
        BusyChanged?.Invoke(this, EventArgs.Empty);
    }
}
