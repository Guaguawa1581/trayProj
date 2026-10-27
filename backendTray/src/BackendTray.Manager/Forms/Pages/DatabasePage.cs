using BackendTray.Contracts;

namespace BackendTray.Manager.Forms.Pages;

/// <summary>
/// 資料庫連線（SQL 驗證）。左邊是「共用預設連線」與各 API 宣告需要的連線，
/// API 的連線可以選擇使用共用設定，或連到不同的 SQL Server。
/// </summary>
internal sealed class DatabasePage : UserControl
{
    private readonly ManagerController _controller;
    private readonly ListBox _slots = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _title;
    private readonly Label _slotHint = Ui.Hint("");
    private readonly CheckBox _useShared = new() { Text = "使用共用預設連線", AutoSize = true, Margin = new Padding(3, 3, 3, 8) };
    private readonly TextBox _server = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "例如 localhost、192.168.1.10\\SQLEXPRESS、db.example.com,1433" };
    private readonly TextBox _database = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox _user = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox _password = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, UseSystemPasswordChar = true };
    private readonly NumericUpDown _timeout = new() { Minimum = 1, Maximum = 300, Width = 70, Anchor = AnchorStyles.Left, TextAlign = HorizontalAlignment.Right };
    private readonly CheckBox _encrypt = new() { Text = "加密連線（Encrypt）", AutoSize = true };
    private readonly CheckBox _trust = new() { Text = "信任伺服器憑證（TrustServerCertificate，自簽憑證時需要）", AutoSize = true };
    private readonly Button _test;
    private readonly Label _testResult = Ui.Hint("");
    private SettingsDto? _draft;
    private bool _binding;

    public DatabasePage(ManagerController controller)
    {
        _controller = controller;

        _title = new Label { AutoSize = true, Margin = new Padding(3, 0, 3, 4) };
        _title.Font = new Font(_title.Font.FontFamily, 11f, FontStyle.Bold);
        _test = Ui.Button("測試連線", async (_, _) => await TestAsync());

        var fields = Ui.FormGrid();
        Ui.AddRow(fields, "SQL Server", _server);
        Ui.AddRow(fields, "資料庫", _database);
        Ui.AddRow(fields, "帳號", _user);
        Ui.AddRow(fields, "密碼", _password);
        Ui.AddRow(fields, "逾時（秒）", _timeout);
        Ui.AddRow(fields, null, _encrypt);
        Ui.AddRow(fields, null, _trust);

        var editor = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(12, 0, 0, 0) };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var control in new Control[]
        {
            _title, _slotHint, _useShared, fields, Ui.Row(_test), _testResult,
            Ui.Hint("連線測試由服務執行，結果反映的是服務實際連線時的網路環境。變更要按下方「儲存設定」才會生效，受影響的 API 會自動重新啟動。"),
        })
        {
            editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editor.Controls.Add(control, 0, editor.RowCount++);
        }

        var editorPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        editorPanel.Controls.Add(editor);

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0) };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(Ui.Label("連線"), 0, 0);
        left.Controls.Add(_slots, 0, 1);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(left, 0, 0);
        root.Controls.Add(editorPanel, 1, 0);
        Controls.Add(root);

        _slots.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding)
                LoadEditor();
        };
        _useShared.CheckedChanged += (_, _) => OnUseSharedChanged();
        foreach (var textBox in new[] { _server, _database, _user, _password })
            textBox.TextChanged += (_, _) => Commit();
        _timeout.ValueChanged += (_, _) => Commit();
        _encrypt.CheckedChanged += (_, _) => Commit();
        _trust.CheckedChanged += (_, _) => Commit();

        LoadEditor();
    }

    public event EventHandler? Changed;

    private Slot? CurrentSlot => _slots.SelectedItem as Slot;

    /// <summary>載入新的草稿（儲存或重新載入後）。盡量保留原本選取的連線。</summary>
    public void Bind(SettingsDto draft)
    {
        var previousKey = CurrentSlot?.Key;
        _draft = draft;

        _binding = true;
        _slots.Items.Clear();
        _slots.Items.Add(new Slot(ConnectionSlots.Shared, "共用預設連線", null));
        foreach (var server in draft.Servers)
        {
            foreach (var connection in server.Connections)
                _slots.Items.Add(new Slot(ConnectionSlots.ForServer(server.Id, connection.Name), $"{server.DisplayName} › {connection.Name}", connection));
        }

        var index = _slots.Items.Cast<Slot>().ToList().FindIndex(slot => slot.Key == previousKey);
        _slots.SelectedIndex = Math.Max(0, index);
        _binding = false;
        LoadEditor();
    }

    /// <summary>右側目前顯示的連線：API 使用共用設定時顯示共用連線（唯讀）。</summary>
    private (DbConnectionDto? Connection, bool Editable) Displayed()
    {
        if (_draft is null || CurrentSlot is not { } slot)
            return (null, false);
        if (slot.ServerConnection is null)
            return (_draft.SharedConnection, true);
        return slot.ServerConnection.UseShared
            ? (_draft.SharedConnection, false)
            : (slot.ServerConnection.Connection, true);
    }

    private void LoadEditor()
    {
        _binding = true;
        try
        {
            var slot = CurrentSlot;
            var (connection, editable) = Displayed();

            _title.Text = slot?.Title ?? "（尚未載入設定）";
            _slotHint.Text = slot?.ServerConnection is null
                ? "各 API 沒有另外設定時使用這組連線。"
                : $"API 透過環境變數 ConnectionStrings__{slot.ServerConnection.Name} 取得這組連線。可使用共用預設連線，或連到不同的 SQL Server。";
            _useShared.Visible = slot?.ServerConnection is not null;
            _useShared.Checked = slot?.ServerConnection?.UseShared ?? false;

            var dto = connection ?? new DbConnectionDto();
            _server.Text = dto.Server;
            _database.Text = dto.Database;
            _user.Text = dto.UserId;
            _password.Text = editable ? dto.NewPassword ?? "" : "";
            _password.PlaceholderText = dto.HasPassword ? "（已儲存，留空表示不變更）" : "";
            _timeout.Value = Math.Clamp(dto.ConnectTimeoutSeconds, (int)_timeout.Minimum, (int)_timeout.Maximum);
            _encrypt.Checked = dto.Encrypt;
            _trust.Checked = dto.TrustServerCertificate;

            foreach (var control in new Control[] { _server, _database, _user, _password, _timeout, _encrypt, _trust })
                control.Enabled = editable;
            _test.Enabled = connection is not null;
            _testResult.Text = "";
        }
        finally
        {
            _binding = false;
        }
    }

    private void OnUseSharedChanged()
    {
        if (_binding || _draft is null || CurrentSlot?.ServerConnection is not { } serverConnection)
            return;

        serverConnection.UseShared = _useShared.Checked;
        if (!serverConnection.UseShared && serverConnection.Connection is null)
        {
            // 從共用設定帶入伺服器與帳號，方便只改資料庫；密碼需要重新輸入。
            var shared = _draft.SharedConnection;
            serverConnection.Connection = new DbConnectionDto
            {
                Server = shared.Server,
                UserId = shared.UserId,
                Encrypt = shared.Encrypt,
                TrustServerCertificate = shared.TrustServerCertificate,
                ConnectTimeoutSeconds = shared.ConnectTimeoutSeconds,
            };
        }

        // 更新清單上的文字（「使用共用」標示）。
        var index = _slots.SelectedIndex;
        _binding = true;
        _slots.Items[index] = _slots.Items[index];
        _slots.SelectedIndex = index;
        _binding = false;

        LoadEditor();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Commit()
    {
        if (_binding)
            return;
        var (connection, editable) = Displayed();
        if (connection is null || !editable)
            return;

        connection.Server = _server.Text.Trim();
        connection.Database = _database.Text.Trim();
        connection.UserId = _user.Text.Trim();
        connection.NewPassword = string.IsNullOrEmpty(_password.Text) ? null : _password.Text;
        connection.ConnectTimeoutSeconds = (int)_timeout.Value;
        connection.Encrypt = _encrypt.Checked;
        connection.TrustServerCertificate = _trust.Checked;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task TestAsync()
    {
        if (CurrentSlot is not { } slot)
            return;
        var (connection, _) = Displayed();
        if (connection is null)
            return;

        var storedPasswordSlot = slot.ServerConnection is { UseShared: false } ? slot.Key : ConnectionSlots.Shared;
        _test.Enabled = false;
        _testResult.ForeColor = Ui.HintColor;
        _testResult.Text = "測試中…";
        try
        {
            var result = await _controller.Client.TestDatabaseAsync(new DbTestRequest
            {
                Connection = connection,
                StoredPasswordSlot = storedPasswordSlot,
            });
            _testResult.ForeColor = result.Success ? Ui.SuccessColor : Ui.ErrorColor;
            _testResult.Text = result.Success
                ? $"✔ {result.Message}{Environment.NewLine}{result.ServerVersion}"
                : $"✖ {result.Message}{Environment.NewLine}{result.Detail}";
        }
        catch (ServiceClientException ex)
        {
            _testResult.ForeColor = Ui.ErrorColor;
            _testResult.Text = $"✖ {ex.Message}";
        }
        finally
        {
            _test.Enabled = true;
        }
    }

    private sealed record Slot(string Key, string Title, ServerConnectionDto? ServerConnection)
    {
        public override string ToString() => ServerConnection is { UseShared: true } ? $"{Title}（使用共用）" : Title;
    }
}
