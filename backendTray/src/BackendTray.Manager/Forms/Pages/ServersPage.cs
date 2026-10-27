using BackendTray.Contracts;

namespace BackendTray.Manager.Forms.Pages;

/// <summary>服務與連接埠：前端（Gateway）與各後端 API 的 port、連線範圍與啟用狀態。</summary>
internal sealed class ServersPage : UserControl
{
    private readonly NumericUpDown _gatewayPort = Ui.PortInput();
    private readonly ComboBox _gatewayListen = Ui.ListenInput();
    private readonly TableLayoutPanel _apiGrid = new() { AutoSize = true, ColumnCount = 5, Dock = DockStyle.Top };
    private SettingsDto? _draft;
    private bool _binding;

    public ServersPage()
    {
        AutoScroll = true;

        var gatewayFields = Ui.FormGrid();
        Ui.AddRow(gatewayFields, "連接埠", _gatewayPort);
        Ui.AddRow(gatewayFields, "允許連線", _gatewayListen);
        Ui.AddRow(gatewayFields, null, Ui.Hint(
            "使用者用瀏覽器連這個 port 開啟網站。/api 開頭的請求由 Gateway 轉給後端 API，前端不需要知道後端的 port。"));

        for (var i = 0; i < 4; i++)
            _apiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _apiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var apiContent = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top };
        apiContent.Controls.Add(_apiGrid, 0, 0);
        apiContent.Controls.Add(Ui.Hint(
            "後端建議只允許本機連線，外部一律經由 Gateway 存取。Swagger 可在伺服器上用 http://localhost:{port}/swagger 開啟。" +
            "停用的 API 不會啟動，Gateway 也不會轉發給它。"), 0, 1);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(Ui.Group("前端（Gateway）", gatewayFields), 0, 0);
        root.Controls.Add(Ui.Group("後端 API", apiContent), 0, 1);
        Controls.Add(root);

        _gatewayPort.ValueChanged += (_, _) => ApplyChange(draft => draft.Gateway.Port = (int)_gatewayPort.Value);
        _gatewayListen.SelectedIndexChanged += (_, _) => ApplyChange(draft => draft.Gateway.Listen = (ListenScope)_gatewayListen.SelectedIndex);
    }

    public event EventHandler? Changed;

    public void Bind(SettingsDto draft)
    {
        _binding = true;
        try
        {
            _draft = draft;
            _gatewayPort.Value = ClampPort(draft.Gateway.Port);
            _gatewayListen.SelectedIndex = (int)draft.Gateway.Listen;
            BuildApiRows(draft.Servers);
        }
        finally
        {
            _binding = false;
        }
    }

    private void BuildApiRows(IReadOnlyList<ServerSettingsDto> servers)
    {
        _apiGrid.SuspendLayout();
        foreach (Control control in _apiGrid.Controls.Cast<Control>().ToList())
            control.Dispose();
        _apiGrid.Controls.Clear();
        _apiGrid.RowStyles.Clear();
        _apiGrid.RowCount = 0;

        AddRow(Ui.Label("啟用", Ui.HintColor), Ui.Label("名稱", Ui.HintColor), Ui.Label("連接埠", Ui.HintColor), Ui.Label("允許連線", Ui.HintColor));
        foreach (var server in servers)
        {
            var enabled = new CheckBox { Checked = server.Enabled, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 6, 12, 3) };
            var port = Ui.PortInput();
            port.Width = LogicalToDeviceUnits(port.Width);
            port.Value = ClampPort(server.Port);
            var listen = Ui.ListenInput();
            listen.Width = LogicalToDeviceUnits(listen.Width);
            listen.SelectedIndex = (int)server.Listen;

            enabled.CheckedChanged += (_, _) => ApplyChange(_ => server.Enabled = enabled.Checked);
            port.ValueChanged += (_, _) => ApplyChange(_ => server.Port = (int)port.Value);
            listen.SelectedIndexChanged += (_, _) => ApplyChange(_ => server.Listen = (ListenScope)listen.SelectedIndex);

            var name = server.Port == server.DefaultPort
                ? $"{server.DisplayName}（{server.Id}）"
                : $"{server.DisplayName}（{server.Id}，預設 port {server.DefaultPort}）";
            AddRow(enabled, Ui.Label(name), port, listen);
        }

        _apiGrid.ResumeLayout(performLayout: true);
    }

    private void AddRow(params Control[] cells)
    {
        var row = _apiGrid.RowCount++;
        _apiGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var column = 0; column < cells.Length; column++)
            _apiGrid.Controls.Add(cells[column], column, row);
    }

    private void ApplyChange(Action<SettingsDto> change)
    {
        if (_binding || _draft is null)
            return;
        change(_draft);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static decimal ClampPort(int port) => Math.Clamp(port, 1, 65535);
}
