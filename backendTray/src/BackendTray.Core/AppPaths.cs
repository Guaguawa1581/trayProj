using BackendTray.Contracts;

namespace BackendTray.Core;

/// <summary>
/// 集中管理路徑。安裝目錄放程式（更新時覆蓋），資料目錄放設定與 log（更新時不動）。
/// </summary>
public sealed class AppPaths
{
    public const string InstallRootVariable = "BACKENDTRAY_INSTALL_ROOT";
    public const string DataRootVariable = "BACKENDTRAY_DATA_ROOT";

    public AppPaths(string installRoot, string dataRoot)
    {
        InstallRoot = Path.GetFullPath(installRoot);
        DataRoot = Path.GetFullPath(dataRoot);
    }

    public string InstallRoot { get; }
    public string DataRoot { get; }

    public string SettingsFile => Path.Combine(DataRoot, "settings.json");
    public string RuntimeDirectory => Path.Combine(DataRoot, "runtime");
    public string LogsDirectory => Path.Combine(DataRoot, "logs");
    public string GatewayConfigFile => Path.Combine(RuntimeDirectory, "gateway.json");
    public string RestartAfterUpdateMarker => Path.Combine(RuntimeDirectory, "restart-after-update");

    public string ServersDirectory => Path.Combine(InstallRoot, "servers");
    public string WebRoot => Path.Combine(InstallRoot, "wwwroot");
    public string GatewayExecutable => Path.Combine(InstallRoot, "gateway", "BackendTray.Gateway.exe");
    public string ManagerExecutable => Path.Combine(InstallRoot, "manager", "BackendTray.Manager.exe");

    /// <summary>
    /// 安裝後服務位於 {安裝目錄}\service\，所以安裝目錄是上一層。
    /// 開發時從 bin 執行，則往上找到 BackendTray.slnx，改用 artifacts\publish 與 artifacts\data。
    /// 兩者都可以用環境變數覆寫。
    /// </summary>
    public static AppPaths FromEnvironment()
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var installRoot = Environment.GetEnvironmentVariable(InstallRootVariable);
        var dataRoot = Environment.GetEnvironmentVariable(DataRootVariable);
        string? repositoryRoot = null;

        if (string.IsNullOrEmpty(installRoot))
        {
            var parent = Path.GetDirectoryName(baseDirectory)!;
            if (Directory.Exists(Path.Combine(parent, "servers")) || Directory.Exists(Path.Combine(parent, "gateway")))
                installRoot = parent;
            else if ((repositoryRoot = FindRepositoryRoot(baseDirectory)) is not null)
                installRoot = Path.Combine(repositoryRoot, "artifacts", "publish");
            else
                installRoot = parent;
        }

        if (string.IsNullOrEmpty(dataRoot))
        {
            dataRoot = repositoryRoot is not null
                ? Path.Combine(repositoryRoot, "artifacts", "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ManagementConstants.ProductName);
        }

        return new AppPaths(installRoot, dataRoot);
    }

    private static string? FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BackendTray.slnx")))
                return directory.FullName;
        }

        return null;
    }
}
