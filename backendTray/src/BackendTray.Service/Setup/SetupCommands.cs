using BackendTray.Core;
using Serilog;

namespace BackendTray.Service.Setup;

/// <summary>
/// 安裝程式呼叫的指令（需要系統管理員權限）：
///   install [--start]   建立或更新 Windows 服務、資料夾權限、防火牆規則與排程工作
///   uninstall           停止並移除服務、防火牆規則與排程工作（保留設定與 log）
///   stop-for-update     更新前停止服務並關閉所有管理介面
/// </summary>
internal static class SetupCommands
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0)
            return false;

        Action<SetupContext>? command = args[0].ToLowerInvariant() switch
        {
            "install" => context => ServiceSetup.Install(context, startNow: args.Contains("--start", StringComparer.OrdinalIgnoreCase)),
            "uninstall" => ServiceSetup.Uninstall,
            "stop-for-update" => ServiceSetup.StopForUpdate,
            _ => null,
        };
        if (command is null)
            return false;

        var paths = AppPaths.FromEnvironment();
        using var log = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(paths.LogsDirectory, "setup-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10)
            .CreateLogger();

        try
        {
            log.Information("執行 {Command}（安裝目錄 {InstallRoot}）", args[0], paths.InstallRoot);
            command(new SetupContext(paths, log));
            log.Information("{Command} 完成", args[0]);
        }
        catch (Exception ex)
        {
            log.Error(ex, "{Command} 失敗", args[0]);
            exitCode = 1;
        }

        return true;
    }
}

internal sealed record SetupContext(AppPaths Paths, Serilog.ILogger Log);
