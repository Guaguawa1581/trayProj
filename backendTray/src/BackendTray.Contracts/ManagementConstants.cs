namespace BackendTray.Contracts;

/// <summary>
/// 服務、管理介面與安裝程式共用的名稱。已經安裝的環境會依賴這些名稱，修改前要考慮升級相容性。
/// </summary>
public static class ManagementConstants
{
    public const string ProductName = "BackendTray";

    public const string ServiceName = "BackendTray";
    public const string ServiceDisplayName = "BackendTray";
    public const string ServiceDescription = "BackendTray 網站與 Web API 伺服器";
    public const string ServiceAccount = @"NT SERVICE\" + ServiceName;

    /// <summary>管理 API 的 named pipe（\\.\pipe\BackendTray.Control）。</summary>
    public const string PipeName = "BackendTray.Control";

    /// <summary>安裝程式更新或移除前，用來通知所有工作階段的管理介面結束。</summary>
    public const string ManagerExitEventName = @"Global\BackendTray.Manager.Exit";

    /// <summary>「登入時開啟管理介面」的工作排程器工作。</summary>
    public const string ManagerTaskName = @"BackendTray\Manager";

    public const string ManagerProcessName = "BackendTray.Manager";

    public const string GatewayServerId = "gateway";
}
