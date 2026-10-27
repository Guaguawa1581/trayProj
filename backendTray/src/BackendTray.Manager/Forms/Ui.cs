using System.ComponentModel;
using BackendTray.Contracts;

namespace BackendTray.Manager;

/// <summary>畫面共用的元件與文字。版面一律用 TableLayoutPanel／FlowLayoutPanel 自動排版，以配合高 DPI。</summary>
internal static class Ui
{
    public static readonly Color WarningColor = Color.FromArgb(176, 96, 0);
    public static readonly Color ErrorColor = Color.FromArgb(198, 40, 40);
    public static readonly Color SuccessColor = Color.FromArgb(46, 125, 50);
    public static readonly Color HintColor = SystemColors.GrayText;

    public static void ShowError(IWin32Window? owner, string title, Exception exception)
    {
        var message = exception is InvalidOperationException { InnerException: Win32Exception inner }
            ? inner.Message
            : exception.Message;
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static bool Confirm(IWin32Window? owner, string message, string title) =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
        == DialogResult.Yes;

    public static Label Label(string text, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 7, 12, 3),
        ForeColor = color ?? SystemColors.ControlText,
    };

    /// <summary>可換行的說明文字。</summary>
    public static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(640, 0),
        ForeColor = HintColor,
        Margin = new Padding(3, 4, 3, 8),
    };

    public static Button Button(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 3, 10, 3),
            Margin = new Padding(0, 3, 8, 3),
            UseVisualStyleBackColor = true,
        };
        button.Click += onClick;
        return button;
    }

    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3),
        };
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>兩欄（標籤＋輸入）的表單。</summary>
    public static TableLayoutPanel FormGrid()
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    public static void AddRow(TableLayoutPanel grid, string? label, Control control)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (label is not null)
            grid.Controls.Add(Label(label), 0, row);
        grid.Controls.Add(control, 1, row);
    }

    public static GroupBox Group(string title, Control content)
    {
        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 0, 0, 10),
        };
        content.Dock = DockStyle.Fill;
        group.Controls.Add(content);
        return group;
    }

    public static NumericUpDown PortInput() => new()
    {
        Minimum = 1,
        Maximum = 65535,
        Width = 90,
        Anchor = AnchorStyles.Left,
        TextAlign = HorizontalAlignment.Right,
    };

    /// <summary>項目順序對應 <see cref="ListenScope"/> 的值。</summary>
    public static ComboBox ListenInput()
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Anchor = AnchorStyles.Left };
        combo.Items.AddRange([Describe(ListenScope.Localhost), Describe(ListenScope.Any)]);
        return combo;
    }

    public static string Describe(ListenScope listen) => listen switch
    {
        ListenScope.Localhost => "僅本機（localhost）",
        _ => "允許區網／外部連線",
    };

    public static string Describe(ServerState state) => state switch
    {
        ServerState.Disabled => "已停用",
        ServerState.Stopped => "已停止",
        ServerState.Starting => "啟動中",
        ServerState.Running => "執行中",
        ServerState.Unhealthy => "健康檢查失敗",
        ServerState.Stopping => "停止中",
        ServerState.Faulted => "異常",
        _ => state.ToString(),
    };
}
