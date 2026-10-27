using BackendTray.Contracts;
using BackendTray.Manager.Platform;

namespace BackendTray.Manager.Forms.Pages;

/// <summary>總覽：服務狀態與啟停、各伺服器狀態、選取伺服器的即時輸出。</summary>
internal sealed class DashboardPage : UserControl
{
    private const int LogLines = 300;

    private readonly ManagerController _controller;
    private readonly Label _stateLabel;
    private readonly Label _detailLabel = Ui.Hint("");
    private readonly Label _warningsLabel = Ui.Hint("");
    private readonly Button _startService;
    private readonly Button _stopService;
    private readonly Button _restartService;
    private readonly Button _pause;
    private readonly Button _resume;
    private readonly ListView _list;
    private readonly Button _restartServer;
    private readonly Button _openSite;
    private readonly Button _openSwagger;
    private readonly Button _openLogs;
    private readonly TextBox _log;
    private string _logText = "";
    private string? _logServerId;
    private bool _loadingLog;

    public DashboardPage(ManagerController controller)
    {
        _controller = controller;

        _stateLabel = new Label { AutoSize = true, Margin = new Padding(3, 0, 3, 2) };
        _stateLabel.Font = new Font(_stateLabel.Font.FontFamily, 13f, FontStyle.Bold);
        _warningsLabel.ForeColor = Ui.WarningColor;

        _startService = Ui.Button("啟動服務", async (_, _) => await controller.RunAsync("啟動服務", ServiceControl.StartAsync, FindForm()));
        _stopService = Ui.Button("停止服務", async (_, _) => await StopServiceAsync());
        _restartService = Ui.Button("重新啟動服務", async (_, _) => await controller.RunAsync("重新啟動服務", ServiceControl.RestartAsync, FindForm()));
        _pause = Ui.Button("暫停所有伺服器", async (_, _) => await controller.RunAsync("暫停", controller.Client.PauseAsync, FindForm()));
        _resume = Ui.Button("繼續", async (_, _) => await controller.RunAsync("繼續", controller.Client.ResumeAsync, FindForm()));

        _list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            Dock = DockStyle.Fill,
        };
        foreach (var (title, width) in new[]
        {
            ("名稱", 160), ("狀態", 100), ("連接埠", 70), ("連線範圍", 150), ("PID", 70), ("啟動時間", 140), ("重啟", 50), ("訊息", 320),
        })
        {
            _list.Columns.Add(title, LogicalToDeviceUnits(width));
        }
        _list.SelectedIndexChanged += async (_, _) =>
        {
            UpdateButtons();
            await RefreshLogAsync();
        };

        _restartServer = Ui.Button("重新啟動選取的伺服器", async (_, _) => await RestartSelectedAsync());
        _openSite = Ui.Button("開啟網站", (_, _) => OpenSite());
        _openSwagger = Ui.Button("開啟 Swagger", (_, _) => OpenSwagger());
        _openLogs = Ui.Button("開啟 log 資料夾", (_, _) => OpenLogs());

        _log = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f),
            BackColor = SystemColors.Window,
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void AddRow(Control control, SizeType sizeType = SizeType.AutoSize, float height = 0)
        {
            root.RowStyles.Add(new RowStyle(sizeType, height));
            root.Controls.Add(control, 0, root.RowCount++);
        }

        AddRow(_stateLabel);
        AddRow(_detailLabel);
        AddRow(Ui.Row(_startService, _stopService, _restartService, new Label { Width = 16 }, _pause, _resume));
        AddRow(_warningsLabel);
        AddRow(_list, SizeType.Percent, 40);
        AddRow(Ui.Row(_restartServer, _openSite, _openSwagger, _openLogs));
        AddRow(Ui.Label("選取伺服器的輸出（最近 300 行）"));
        AddRow(_log, SizeType.Percent, 60);
        Controls.Add(root);

        controller.StatusChanged += async (_, _) =>
        {
            UpdateView();
            await RefreshLogAsync();
        };
        controller.BusyChanged += (_, _) => UpdateButtons();
        VisibleChanged += async (_, _) => await RefreshLogAsync();
        UpdateView();
    }

    private ServerStatusDto? SelectedServer =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as ServerStatusDto : null;

    private void UpdateView()
    {
        var status = _controller.Status;
        _stateLabel.Text = status.Summary;
        _stateLabel.ForeColor = status.State switch
        {
            TrayState.Running => Ui.SuccessColor,
            TrayState.Error or TrayState.AccessDenied => Ui.ErrorColor,
            TrayState.Warning or TrayState.Starting or TrayState.Transitioning => Ui.WarningColor,
            _ => SystemColors.ControlText,
        };
        _detailLabel.Text = status.Service is { } service
            ? $"服務版本 {service.Version}・啟動於 {service.StartedAt:yyyy-MM-dd HH:mm:ss}・安裝目錄 {service.InstallRoot}"
            : status.Error ?? "";

        var warnings = status.Service?.Warnings ?? [];
        _warningsLabel.Text = string.Join(Environment.NewLine, warnings.Select(warning => "⚠ " + warning));
        _warningsLabel.Visible = warnings.Count > 0;

        UpdateServers(status.Service?.Servers ?? []);
        UpdateButtons();
    }

    private void UpdateServers(IReadOnlyList<ServerStatusDto> servers)
    {
        _list.BeginUpdate();
        try
        {
            foreach (var server in servers)
            {
                var item = _list.Items[server.Id] ?? _list.Items.Add(new ListViewItem(new string[_list.Columns.Count]) { Name = server.Id });
                item.Tag = server;
                item.Text = server.DisplayName;
                item.SubItems[1].Text = Ui.Describe(server.State);
                item.SubItems[2].Text = server.Port.ToString();
                item.SubItems[3].Text = Ui.Describe(server.Listen);
                item.SubItems[4].Text = server.ProcessId?.ToString() ?? "";
                item.SubItems[5].Text = server.StartedAt?.ToString("MM-dd HH:mm:ss") ?? "";
                item.SubItems[6].Text = server.RestartCount > 0 ? server.RestartCount.ToString() : "";
                item.SubItems[7].Text = server.Message ?? "";
                item.ForeColor = server.State switch
                {
                    ServerState.Faulted or ServerState.Unhealthy => Ui.ErrorColor,
                    ServerState.Starting or ServerState.Stopping => Ui.WarningColor,
                    ServerState.Disabled or ServerState.Stopped => Ui.HintColor,
                    _ => SystemColors.WindowText,
                };
            }

            for (var i = _list.Items.Count - 1; i >= 0; i--)
            {
                if (servers.All(server => server.Id != _list.Items[i].Name))
                    _list.Items.RemoveAt(i);
            }

            if (_list.SelectedItems.Count == 0 && _list.Items.Count > 0)
                _list.Items[0].Selected = true;
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    private void UpdateButtons()
    {
        var status = _controller.Status;
        var busy = _controller.Busy;
        var scm = status.ScmStatus;
        var selected = SelectedServer;

        _startService.Enabled = !busy && scm == System.ServiceProcess.ServiceControllerStatus.Stopped;
        _stopService.Enabled = !busy && scm is not null and not System.ServiceProcess.ServiceControllerStatus.Stopped;
        _restartService.Enabled = !busy && scm == System.ServiceProcess.ServiceControllerStatus.Running;
        _pause.Enabled = !busy && _controller.PrimaryAction == PrimaryAction.Pause;
        _resume.Enabled = !busy && _controller.PrimaryAction == PrimaryAction.Resume;

        _restartServer.Enabled = !busy && status.Service is { Paused: false } && selected is { State: not ServerState.Disabled };
        _openSite.Enabled = FindGateway() is not null;
        _openSwagger.Enabled = selected is { Kind: ServerKind.Api };
        _openLogs.Enabled = status.Service is not null;
    }

    private async Task RefreshLogAsync()
    {
        if (!Visible || _loadingLog)
            return;

        var selected = SelectedServer;
        if (selected is null || !_controller.Status.IsReachable)
            return;

        _loadingLog = true;
        try
        {
            var log = await _controller.Client.GetLogAsync(selected.Id, LogLines);
            var text = string.Join(Environment.NewLine, log.Lines);
            if (text != _logText || selected.Id != _logServerId)
            {
                _logText = text;
                _logServerId = selected.Id;
                _log.Text = text;
                _log.SelectionStart = _log.TextLength;
                _log.ScrollToCaret();
            }
        }
        catch (ServiceClientException)
        {
            // 下一次狀態更新時再試。
        }
        finally
        {
            _loadingLog = false;
        }
    }

    private async Task StopServiceAsync()
    {
        const string message = "停止服務後，所有網站與 API 都會停止，影響所有使用者。\n\n確定要停止嗎？";
        if (Ui.Confirm(FindForm(), message, "停止服務"))
            await _controller.RunAsync("停止服務", ServiceControl.StopAsync, FindForm());
    }

    private async Task RestartSelectedAsync()
    {
        if (SelectedServer is { } server)
            await _controller.RunAsync($"重新啟動 {server.DisplayName}", () => _controller.Client.RestartServerAsync(server.Id), FindForm());
    }

    private ServerStatusDto? FindGateway() =>
        _controller.Status.Service?.Servers.FirstOrDefault(server => server.Kind == ServerKind.Gateway);

    private void OpenSite()
    {
        if (FindGateway() is { } gateway)
            ProcessRunner.OpenInShell($"http://localhost:{gateway.Port}/");
    }

    private void OpenSwagger()
    {
        if (SelectedServer is { Kind: ServerKind.Api } server)
            ProcessRunner.OpenInShell($"http://localhost:{server.Port}/swagger");
    }

    private void OpenLogs()
    {
        if (_controller.Status.Service is { } service)
            ProcessRunner.OpenInShell(service.LogsDirectory);
    }
}
