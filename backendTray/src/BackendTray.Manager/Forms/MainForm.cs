using System.Text.Json;
using BackendTray.Contracts;
using BackendTray.Manager.Forms.Pages;

namespace BackendTray.Manager.Forms;

/// <summary>
/// 主畫面（設定視窗）。按右上角 X 只會隱藏，真正關閉只透過系統匣的「結束」。
/// 「資料庫」與「服務與連接埠」兩頁共用同一份草稿，由底部的「儲存設定」一次送出。
/// </summary>
internal sealed class MainForm : Form
{
    private readonly ManagerController _controller;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly DatabasePage _database;
    private readonly ServersPage _servers = new() { Dock = DockStyle.Fill };
    private readonly TabPage _databaseTab;
    private readonly TabPage _serversTab;
    private readonly TableLayoutPanel _settingsBar;
    private readonly Label _settingsStatus = Ui.Label("");
    private readonly Button _saveButton;
    private readonly Button _reloadButton;
    private SettingsDto? _draft;
    private string? _baseline;
    private string _lastMessage = "";
    private bool _loadingSettings;
    private bool _saving;
    private bool _allowClose;

    public MainForm(ManagerController controller)
    {
        _controller = controller;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "BackendTray 管理介面";
        Icon = TrayIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 660);
        MinimumSize = new Size(820, 560);

        _database = new DatabasePage(controller) { Dock = DockStyle.Fill };
        _databaseTab = CreateTab("資料庫", _database);
        _serversTab = CreateTab("服務與連接埠", _servers);
        _tabs.TabPages.Add(CreateTab("總覽", new DashboardPage(controller) { Dock = DockStyle.Fill }));
        _tabs.TabPages.Add(_databaseTab);
        _tabs.TabPages.Add(_serversTab);
        _tabs.TabPages.Add(CreateTab("一般", new GeneralPage(controller) { Dock = DockStyle.Fill }));
        _tabs.SelectedIndexChanged += async (_, _) => await OnTabChangedAsync();

        _saveButton = Ui.Button("儲存設定", async (_, _) => await SaveAsync());
        _reloadButton = Ui.Button("重新載入", async (_, _) => await ReloadAsync());
        _settingsBar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(12, 6, 4, 8),
        };
        _settingsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _settingsBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _settingsBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _settingsBar.Controls.Add(_settingsStatus, 0, 0);
        _settingsBar.Controls.Add(_reloadButton, 1, 0);
        _settingsBar.Controls.Add(_saveButton, 2, 0);

        // 最後加入的控制項最先停駐：設定列在底部，分頁填滿其餘空間。
        Controls.Add(_tabs);
        Controls.Add(_settingsBar);
        ResumeLayout(performLayout: true);

        _database.Changed += (_, _) => UpdateSettingsBar();
        _servers.Changed += (_, _) => UpdateSettingsBar();
        controller.StatusChanged += async (_, _) => await OnStatusChangedAsync();
        UpdateSettingsBar();
    }

    private bool IsDirty => _draft is not null && Serialize(_draft) != _baseline;

    public void ShowAndActivate()
    {
        if (!Visible)
            Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    /// <summary>由系統匣的「結束」呼叫，真正關閉視窗。</summary>
    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 按 X 只隱藏；Windows 登出/關機等其他原因則正常關閉。
        if (e.CloseReason == CloseReason.UserClosing && !_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private static TabPage CreateTab(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(10), UseVisualStyleBackColor = true };
        page.Controls.Add(content);
        return page;
    }

    private static string Serialize(SettingsDto settings) => JsonSerializer.Serialize(settings, ManagementJson.Options);

    private async Task OnStatusChangedAsync()
    {
        if (_controller.Status.IsReachable && _draft is null)
            await LoadSettingsAsync();
        UpdateSettingsBar();
    }

    private async Task OnTabChangedAsync()
    {
        UpdateSettingsBar();

        // 切到設定頁時取得最新設定（可能有其他管理員改過），但不覆蓋尚未儲存的變更。
        if (IsSettingsTabSelected && !IsDirty && _controller.Status.IsReachable)
            await LoadSettingsAsync();
    }

    private bool IsSettingsTabSelected => _tabs.SelectedTab == _databaseTab || _tabs.SelectedTab == _serversTab;

    private async Task LoadSettingsAsync()
    {
        if (_loadingSettings || _saving)
            return;

        _loadingSettings = true;
        try
        {
            var settings = await _controller.Client.GetSettingsAsync();
            _draft = settings;
            _baseline = Serialize(settings);
            _database.Bind(settings);
            _servers.Bind(settings);
        }
        catch (ServiceClientException)
        {
            // 設定列會顯示無法連線。
        }
        finally
        {
            _loadingSettings = false;
            UpdateSettingsBar();
        }
    }

    private async Task ReloadAsync()
    {
        if (IsDirty && !Ui.Confirm(this, "重新載入會捨棄尚未儲存的變更。確定嗎？", "重新載入設定"))
            return;
        _lastMessage = "";
        await LoadSettingsAsync();
    }

    private async Task SaveAsync()
    {
        if (_draft is null)
            return;

        _saving = true;
        UpdateSettingsBar();
        try
        {
            var result = await _controller.Client.SaveSettingsAsync(_draft);
            switch (result.Outcome)
            {
                case SaveOutcome.Saved:
                    _lastMessage = result.RestartedServers.Count > 0
                        ? $"已儲存，並重新啟動：{string.Join("、", result.RestartedServers)}。"
                        : "已儲存。";
                    _saving = false;
                    await LoadSettingsAsync();
                    await _controller.RefreshAsync();
                    break;

                case SaveOutcome.Conflict:
                    if (Ui.Confirm(this, "設定已被其他管理員修改。要重新載入最新設定嗎？（你的變更會遺失）", "設定衝突"))
                    {
                        _lastMessage = "";
                        _saving = false;
                        await LoadSettingsAsync();
                    }
                    break;

                default:
                    MessageBox.Show(this, string.Join(Environment.NewLine, result.Errors.Select(error => "• " + error)),
                        "設定有誤，尚未儲存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
            }
        }
        catch (ServiceClientException ex)
        {
            Ui.ShowError(this, "儲存設定失敗", ex);
        }
        finally
        {
            _saving = false;
            UpdateSettingsBar();
        }
    }

    private void UpdateSettingsBar()
    {
        var reachable = _controller.Status.IsReachable;
        var editable = reachable && _draft is not null && !_saving;
        _database.Enabled = editable;
        _servers.Enabled = editable;

        _settingsBar.Visible = IsSettingsTabSelected;
        _saveButton.Enabled = editable && IsDirty;
        _reloadButton.Enabled = reachable && !_saving;

        if (!reachable)
        {
            _settingsStatus.Text = "無法連線到服務，暫時無法讀取或修改設定。";
            _settingsStatus.ForeColor = Ui.ErrorColor;
        }
        else if (_saving)
        {
            _settingsStatus.Text = "儲存中…";
            _settingsStatus.ForeColor = Ui.HintColor;
        }
        else if (IsDirty)
        {
            _settingsStatus.Text = "有尚未儲存的變更。";
            _settingsStatus.ForeColor = Ui.WarningColor;
        }
        else
        {
            _settingsStatus.Text = _lastMessage;
            _settingsStatus.ForeColor = Ui.SuccessColor;
        }
    }
}
