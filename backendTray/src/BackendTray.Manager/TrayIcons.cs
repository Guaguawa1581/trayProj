using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BackendTray.Manager;

/// <summary>依狀態產生系統匣圖示（圓角方塊加上 B），不需要額外的 .ico 檔。</summary>
internal static class TrayIcons
{
    private static readonly Color Green = Color.FromArgb(46, 125, 50);
    private static readonly Color Amber = Color.FromArgb(230, 145, 0);
    private static readonly Color Gray = Color.FromArgb(117, 117, 117);
    private static readonly Color Red = Color.FromArgb(198, 40, 40);

    private static readonly Dictionary<TrayState, Icon> Cache = [];

    public static Icon Application => For(TrayState.Running);

    public static Icon For(TrayState state)
    {
        if (Cache.TryGetValue(state, out var icon))
            return icon;

        icon = state switch
        {
            TrayState.Running => Create(Green, crossed: false),
            TrayState.Starting or TrayState.Warning or TrayState.Transitioning => Create(Amber, crossed: false),
            TrayState.Error => Create(Red, crossed: false),
            TrayState.Paused or TrayState.Unknown => Create(Gray, crossed: false),
            _ => Create(Gray, crossed: true),
        };
        Cache[state] = icon;
        return icon;
    }

    private static Icon Create(Color color, bool crossed)
    {
        var size = SystemInformation.SmallIconSize.Width;
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            var radius = size / 4f;
            using (var path = RoundedRectangle(new RectangleF(0.5f, 0.5f, size - 1, size - 1), radius))
            using (var brush = new SolidBrush(color))
                graphics.FillPath(brush, path);

            using (var font = new Font("Segoe UI", size * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                graphics.DrawString("B", font, Brushes.White, new RectangleF(0, 0, size, size + 1), format);

            if (crossed)
            {
                using var pen = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(1.5f, size / 10f));
                graphics.DrawLine(pen, size * 0.15f, size * 0.15f, size * 0.85f, size * 0.85f);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
