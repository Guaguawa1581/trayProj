using BackendTray.Manager.Forms;
using BackendTray.Manager.Platform;

namespace BackendTray.Manager;

/// <summary>
/// 系統匣圖示與右鍵選單。左鍵單擊或雙擊開啟主畫面；按主畫面的 X 只會隱藏視窗。
/// 「結束」會停止 Windows 服務並關閉管理介面；登出或關機時只會關閉管理介面，服務照常執行。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Control _invoker = new();
    private readonly ManagerController _controller;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _primaryItem;
    private readonly ToolStripMenuItem _exitItem;
    private MainForm? _mainForm;
    private TrayState _lastState = TrayState.Unknown;
    private bool _exiting;

    public TrayApplicationContext(SingleInstance instance, bool startHidden)
    {
        // 強制建立 handle，讓背景執行緒可以透過它切回 UI 執行緒。
        _ = _invoker.Handle;

        _controller = new ManagerController(new ServiceClient());
        _controller.StatusChanged += (_, _) => UpdateTray();
        _controller.BusyChanged += (_, _) => UpdateTray();

        var menu = new ContextMenuStrip();
        _openItem = new ToolStripMenuItem("開啟主畫面", null, (_, _) => ShowMainWindow());
        _openItem.Font = new Font(menu.Font, FontStyle.Bold);
        _primaryItem = new ToolStripMenuItem("暫停服務", null, async (_, _) => await _controller.RunPrimaryActionAsync(null));
        _exitItem = new ToolStripMenuItem("結束", null, async (_, _) => await ExitAsync());
        menu.Items.AddRange([_openItem, new ToolStripSeparator(), _primaryItem, new ToolStripSeparator(), _exitItem]);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = TrayIcons.For(TrayState.Unknown),
            Text = "BackendTray",
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowMainWindow();
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        instance.Listen(
            onShow: () => _invoker.BeginInvoke(ShowMainWindow),
            onExit: () => _invoker.BeginInvoke(CloseManager));

        UpdateTray();
        _controller.Start();
        if (!startHidden)
            ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_exiting)
            return;
        _mainForm ??= new MainForm(_controller);
        _mainForm.ShowAndActivate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _mainForm?.Dispose();
            _controller.Dispose();
            _invoker.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateTray()
    {
        var status = _controller.Status;
        _notifyIcon.Icon = TrayIcons.For(status.State);

        var text = $"BackendTray：{status.Summary}";
        _notifyIcon.Text = text.Length > 127 ? text[..127] : text;

        _primaryItem.Text = _controller.PrimaryAction switch
        {
            PrimaryAction.Resume => "繼續服務",
            PrimaryAction.StartService => "啟動服務",
            _ => "暫停服務",
        };
        _primaryItem.Enabled = !_controller.Busy && _controller.PrimaryAction != PrimaryAction.None;
        _exitItem.Enabled = !_controller.Busy;

        if (status.State == TrayState.Error && _lastState != TrayState.Error)
        {
            var failed = status.Service?.Servers
                .Where(s => s.State is Contracts.ServerState.Faulted or Contracts.ServerState.Unhealthy)
                .Select(s => s.DisplayName) ?? [];
            _notifyIcon.ShowBalloonTip(5000, "BackendTray", $"伺服器發生異常：{string.Join("、", failed)}", ToolTipIcon.Warning);
        }

        _lastState = status.State;
    }

    /// <summary>「結束」：停止 Windows 服務（影響所有使用者），然後關閉管理介面。</summary>
    private async Task ExitAsync()
    {
        var scmStatus = ServiceControl.QueryStatus();
        if (scmStatus is not null and not System.ServiceProcess.ServiceControllerStatus.Stopped)
        {
            const string message = "結束會停止 BackendTray 服務，所有網站與 API 都會停止，影響所有使用者。\n\n確定要結束嗎？";
            if (!Ui.Confirm(_mainForm is { Visible: true } ? _mainForm : null, message, "結束 BackendTray"))
                return;

            _notifyIcon.Text = "BackendTray：正在停止服務…";
            var stopped = await _controller.RunAsync("停止服務", ServiceControl.StopAsync, null);
            if (!stopped && !Ui.Confirm(null, "服務沒有成功停止。仍要關閉管理介面嗎？", "結束 BackendTray"))
                return;
        }

        CloseManager();
    }

    /// <summary>只關閉管理介面，不影響服務（安裝程式更新時也會走這裡）。</summary>
    private void CloseManager()
    {
        if (_exiting)
            return;
        _exiting = true;
        _mainForm?.CloseForExit();
        _notifyIcon.Visible = false;
        ExitThread();
    }
}
