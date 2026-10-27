using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Xml.Linq;
using BackendTray.Contracts;
using BackendTray.Core.Servers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BackendTray.Service.Setup;

internal static class ServiceSetup
{
    private const string ServiceName = ManagementConstants.ServiceName;
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(60);

    public static void Install(SetupContext context, bool startNow)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("無法取得服務執行檔路徑。");
        var existed = ServiceExists();

        if (existed)
        {
            // 只更新路徑，保留管理員設定的啟動類型與帳戶。
            context.Log.Information("更新服務設定");
            RunSc(context, "config", ServiceName, "binPath=", Quote(executable));
        }
        else
        {
            context.Log.Information("建立服務（帳戶 {Account}，自動延遲啟動）", ManagementConstants.ServiceAccount);
            RunSc(context, "create", ServiceName,
                "binPath=", Quote(executable),
                "start=", "delayed-auto",
                "obj=", ManagementConstants.ServiceAccount,
                "DisplayName=", ManagementConstants.ServiceDisplayName);
        }

        RunSc(context, "description", ServiceName, ManagementConstants.ServiceDescription);
        RunSc(context, "failure", ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/10000/restart/60000");
        RunSc(context, "sidtype", ServiceName, "unrestricted");

        SecureDataDirectory(context);
        ConfigureFirewall(context);
        EnsureManagerTask(context);

        var marker = context.Paths.RestartAfterUpdateMarker;
        var wasRunningBeforeUpdate = File.Exists(marker);
        if (startNow || !existed || wasRunningBeforeUpdate)
            StartService(context);
        if (wasRunningBeforeUpdate)
            File.Delete(marker);
    }

    public static void Uninstall(SetupContext context)
    {
        CloseManagers(context);

        if (ServiceExists())
        {
            StopService(context);
            RunSc(context, "delete", ServiceName);
        }

        foreach (var (name, _) in FirewallRules(context))
            Run(context, "netsh", ["advfirewall", "firewall", "delete", "rule", $"name={name}"], allowFailure: true);

        Run(context, "schtasks", ["/Delete", "/TN", ManagementConstants.ManagerTaskName, "/F"], allowFailure: true);
    }

    /// <summary>記錄服務是否在執行（更新後據此決定要不要重新啟動），然後停止服務並關閉所有管理介面。</summary>
    public static void StopForUpdate(SetupContext context)
    {
        if (ServiceExists())
        {
            using var service = new ServiceController(ServiceName);
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                Directory.CreateDirectory(context.Paths.RuntimeDirectory);
                File.WriteAllText(context.Paths.RestartAfterUpdateMarker, DateTimeOffset.Now.ToString("O"));
                StopService(context);
            }
        }

        CloseManagers(context);
    }

    private static bool ServiceExists() =>
        ServiceController.GetServices().Any(s => string.Equals(s.ServiceName, ServiceName, StringComparison.OrdinalIgnoreCase));

    private static void StartService(SetupContext context)
    {
        using var service = new ServiceController(ServiceName);
        if (service.Status == ServiceControllerStatus.Running)
            return;
        context.Log.Information("啟動服務");
        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
    }

    private static void StopService(SetupContext context)
    {
        using var service = new ServiceController(ServiceName);
        if (service.Status == ServiceControllerStatus.Stopped)
            return;
        context.Log.Information("停止服務");
        if (service.Status != ServiceControllerStatus.StopPending)
            service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
    }

    /// <summary>資料目錄只允許 SYSTEM、Administrators 與服務帳戶存取（裡面有加密的資料庫密碼）。</summary>
    private static void SecureDataDirectory(SetupContext context)
    {
        var directory = Directory.CreateDirectory(context.Paths.DataRoot);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new NTAccount(ManagementConstants.ServiceAccount), FileSystemRights.Modify, inherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(security);
        context.Log.Information("已設定 {Directory} 的存取權限", directory.FullName);
    }

    /// <summary>依程式路徑允許連入（網域與私人網路），之後修改 port 不必重設規則。</summary>
    private static void ConfigureFirewall(SetupContext context)
    {
        foreach (var (name, program) in FirewallRules(context))
        {
            Run(context, "netsh", ["advfirewall", "firewall", "delete", "rule", $"name={name}"], allowFailure: true);
            Run(context, "netsh",
            [
                "advfirewall", "firewall", "add", "rule", $"name={name}", "dir=in", "action=allow",
                $"program={program}", "enable=yes", "profile=domain,private",
            ]);
        }
    }

    private static IEnumerable<(string Name, string Program)> FirewallRules(SetupContext context)
    {
        yield return ($"{ManagementConstants.ProductName}-{ManagementConstants.GatewayServerId}", context.Paths.GatewayExecutable);
        var catalog = new ServerCatalog(context.Paths, NullLogger<ServerCatalog>.Instance);
        foreach (var server in catalog.Servers)
            yield return ($"{ManagementConstants.ProductName}-{server.Id}", server.ExecutablePath);
    }

    /// <summary>
    /// 任何系統管理員登入時，以最高權限開啟管理介面（Run 機碼無法自動啟動需要提升權限的程式）。
    /// 已存在時保留管理員設定的啟用狀態。
    /// </summary>
    private static void EnsureManagerTask(SetupContext context)
    {
        if (Run(context, "schtasks", ["/Query", "/TN", ManagementConstants.ManagerTaskName], allowFailure: true) == 0)
            return;

        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var task = new XDocument(
            new XElement(ns + "Task", new XAttribute("version", "1.3"),
                new XElement(ns + "RegistrationInfo",
                    new XElement(ns + "Description", "登入時開啟 BackendTray 管理介面")),
                new XElement(ns + "Triggers",
                    new XElement(ns + "LogonTrigger", new XElement(ns + "Enabled", "true"))),
                new XElement(ns + "Principals",
                    new XElement(ns + "Principal", new XAttribute("id", "Author"),
                        new XElement(ns + "GroupId", "S-1-5-32-544"),
                        new XElement(ns + "RunLevel", "HighestAvailable"))),
                new XElement(ns + "Settings",
                    new XElement(ns + "MultipleInstancesPolicy", "Parallel"),
                    new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                    new XElement(ns + "StopIfGoingOnBatteries", "false"),
                    new XElement(ns + "ExecutionTimeLimit", "PT0S"),
                    new XElement(ns + "Priority", "5"),
                    new XElement(ns + "Enabled", "true")),
                new XElement(ns + "Actions", new XAttribute("Context", "Author"),
                    new XElement(ns + "Exec",
                        new XElement(ns + "Command", context.Paths.ManagerExecutable),
                        new XElement(ns + "Arguments", "--autostart")))));

        var file = Path.Combine(Path.GetTempPath(), $"BackendTray-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, task.ToString(), Encoding.Unicode);
            Run(context, "schtasks", ["/Create", "/TN", ManagementConstants.ManagerTaskName, "/XML", file, "/F"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>通知所有工作階段的管理介面結束，5 秒內沒結束的強制關閉（更新時要取代它的檔案）。</summary>
    private static void CloseManagers(SetupContext context)
    {
        EventWaitHandle? exitEvent = null;
        try
        {
            if (EventWaitHandle.TryOpenExisting(ManagementConstants.ManagerExitEventName, out exitEvent))
                exitEvent.Set();

            var deadline = DateTime.UtcNow.AddSeconds(5);
            foreach (var process in Process.GetProcessesByName(ManagementConstants.ManagerProcessName))
            {
                using (process)
                {
                    var remaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                    if (process.WaitForExit(remaining))
                        continue;
                    context.Log.Warning("管理介面（PID {Pid}）沒有回應，強制結束", process.Id);
                    try
                    {
                        Process.EnterDebugMode();
                        process.Kill();
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        context.Log.Warning(ex, "無法結束管理介面（PID {Pid}）", process.Id);
                    }
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Log.Warning(ex, "無法通知管理介面結束");
        }
        finally
        {
            // 手動重設的事件若一直保持已觸發，之後開啟的管理介面會立刻結束。
            exitEvent?.Reset();
            exitEvent?.Dispose();
        }
    }

    private static string Quote(string path) => $"\"{path}\"";

    private static void RunSc(SetupContext context, params string[] arguments) => Run(context, "sc.exe", arguments);

    private static int Run(SetupContext context, string fileName, IReadOnlyList<string> arguments, bool allowFailure = false)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"無法執行 {fileName}。");
        var standardError = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd() + standardError.GetAwaiter().GetResult();
        process.WaitForExit();

        context.Log.Debug("{File} {Arguments} → {ExitCode}: {Output}", fileName, string.Join(' ', arguments), process.ExitCode, output.Trim());
        if (process.ExitCode != 0 && !allowFailure)
            throw new InvalidOperationException($"{fileName} {string.Join(' ', arguments)} 失敗（代碼 {process.ExitCode}）：{output.Trim()}");
        return process.ExitCode;
    }
}
