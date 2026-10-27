namespace BackendTray.Manager;

internal static class Program
{
    /// <param name="args">--autostart：登入時由工作排程器啟動，只顯示系統匣圖示、不開視窗。</param>
    [STAThread]
    private static void Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            // 同一個工作階段已經有管理介面：請它顯示視窗後結束。
            SingleInstance.ActivateExisting();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Ui.ShowError(null, "發生未預期的錯誤", e.Exception);

        var startHidden = args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
        using var context = new TrayApplicationContext(instance, startHidden);
        Application.Run(context);
    }
}
