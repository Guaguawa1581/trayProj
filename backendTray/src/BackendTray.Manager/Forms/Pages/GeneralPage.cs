using System.Reflection;
using BackendTray.Manager.Platform;

namespace BackendTray.Manager.Forms.Pages;

/// <summary>一般：服務的啟動類型（開機自動啟動）、登入時開啟管理介面、版本與路徑。這些設定會立即生效。</summary>
internal sealed class GeneralPage : UserControl
{
    private readonly ManagerController _controller;
    private readonly ComboBox _startupMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Anchor = AnchorStyles.Left };
    private readonly Button _applyStartup;
    private readonly Label _startupStatus = Ui.Hint("");
    private readonly CheckBox _managerAutoStart = new() { Text = "Windows 登入時自動開啟管理介面（套用到所有系統管理員）", AutoSize = true };
    private readonly Label _managerAutoStartStatus = Ui.Hint("");
    private readonly Label _managerVersion = Ui.Label("");
    private readonly Label _serviceVersion = Ui.Label("");
    private readonly TextBox _installRoot = PathBox();
    private readonly TextBox _dataRoot = PathBox();
    private readonly Button _openDataRoot;
    private bool _binding;

    public GeneralPage(ManagerController controller)
    {
        _controller = controller;
        AutoScroll = true;

        // 順序對應 ServiceStartupMode。
        _startupMode.Items.AddRange(["自動（延遲啟動）", "自動", "手動", "停用"]);
        _applyStartup = Ui.Button("套用", async (_, _) => await ApplyStartupModeAsync());
        _openDataRoot = Ui.Button("開啟資料目錄", (_, _) =>
        {
            if (_controller.Status.Service is { } service)
                ProcessRunner.OpenInShell(service.DataRoot);
        });

        var startupFields = Ui.FormGrid();
        Ui.AddRow(startupFields, "啟動類型", Ui.Row(_startupMode, _applyStartup));
        Ui.AddRow(startupFields, null, _startupStatus);
        Ui.AddRow(startupFields, null, Ui.Hint(
            "「自動（延遲啟動）」會在開機後稍晚啟動，讓同一台電腦上的 SQL Server 先就緒。服務不需要任何人登入就會執行。"));

        var managerFields = Ui.FormGrid();
        Ui.AddRow(managerFields, null, _managerAutoStart);
        Ui.AddRow(managerFields, null, _managerAutoStartStatus);

        var aboutFields = Ui.FormGrid();
        Ui.AddRow(aboutFields, "管理介面版本", _managerVersion);
        Ui.AddRow(aboutFields, "服務版本", _serviceVersion);
        Ui.AddRow(aboutFields, "安裝目錄", _installRoot);
        Ui.AddRow(aboutFields, "資料目錄", _dataRoot);
        Ui.AddRow(aboutFields, null, Ui.Row(_openDataRoot));

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(Ui.Group("Windows 服務（開機自動啟動）", startupFields), 0, 0);
        root.Controls.Add(Ui.Group("管理介面", managerFields), 0, 1);
        root.Controls.Add(Ui.Group("關於", aboutFields), 0, 2);
        Controls.Add(root);

        _managerVersion.Text = typeof(GeneralPage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        _managerAutoStart.CheckedChanged += async (_, _) => await ApplyManagerAutoStartAsync();
        controller.StatusChanged += (_, _) => UpdateInfo();
        VisibleChanged += async (_, _) =>
        {
            if (Visible)
                await RefreshAsync();
        };
        UpdateInfo();
    }

    /// <summary>路徑用唯讀文字框顯示：不會折行，也可以選取複製。</summary>
    private static TextBox PathBox() => new()
    {
        ReadOnly = true,
        TabStop = false,
        BorderStyle = BorderStyle.None,
        BackColor = SystemColors.Control,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Margin = new Padding(3, 7, 3, 3),
    };

    private void UpdateInfo()
    {
        var service = _controller.Status.Service;
        _serviceVersion.Text = service?.Version ?? "（無法連線到服務）";
        _installRoot.Text = service?.InstallRoot ?? "";
        _dataRoot.Text = service?.DataRoot ?? "";
        _openDataRoot.Enabled = service is not null;
    }

    private async Task RefreshAsync()
    {
        var mode = await Task.Run(ServiceControl.GetStartupMode);
        bool? autoStart;
        try
        {
            autoStart = await ManagerAutoStart.IsEnabledAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException)
        {
            autoStart = null;
        }

        _binding = true;
        try
        {
            _startupMode.Enabled = _applyStartup.Enabled = mode is not null;
            _startupMode.SelectedIndex = mode is { } current ? (int)current : -1;
            _startupStatus.Text = mode is null ? "服務尚未安裝。" : "";

            _managerAutoStart.Enabled = autoStart is not null;
            _managerAutoStart.Checked = autoStart ?? false;
            _managerAutoStartStatus.Text = autoStart is null ? "找不到排程工作，請重新執行安裝程式。" : "";
        }
        finally
        {
            _binding = false;
        }
    }

    private async Task ApplyStartupModeAsync()
    {
        if (_startupMode.SelectedIndex < 0)
            return;
        var mode = (ServiceStartupMode)_startupMode.SelectedIndex;
        if (await _controller.RunAsync("變更啟動類型", () => ServiceControl.SetStartupModeAsync(mode), FindForm()))
            _startupStatus.Text = "已套用。";
        await RefreshAsync();
    }

    private async Task ApplyManagerAutoStartAsync()
    {
        if (_binding)
            return;
        var enabled = _managerAutoStart.Checked;
        await _controller.RunAsync("變更自動開啟設定", () => ManagerAutoStart.SetEnabledAsync(enabled), FindForm());
        await RefreshAsync();
    }
}
