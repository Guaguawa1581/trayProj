using System.ServiceProcess;
using BackendTray.Contracts;
using Microsoft.Win32;

namespace BackendTray.Manager.Platform;

internal enum ServiceStartupMode
{
    AutomaticDelayed,
    Automatic,
    Manual,
    Disabled,
}

/// <summary>透過 SCM 控制 Windows 服務（需要系統管理員權限）。</summary>
internal static class ServiceControl
{
    private const string Name = ManagementConstants.ServiceName;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    /// <summary>服務未安裝時回傳 null。</summary>
    public static ServiceControllerStatus? QueryStatus()
    {
        try
        {
            using var service = new ServiceController(Name);
            return service.Status;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static Task StartAsync() => Task.Run(() =>
    {
        using var service = new ServiceController(Name);
        if (service.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
        {
            service.WaitForStatus(ServiceControllerStatus.Stopped, Timeout);
            service.Start();
        }
        service.WaitForStatus(ServiceControllerStatus.Running, Timeout);
    });

    public static Task StopAsync() => Task.Run(() =>
    {
        using var service = new ServiceController(Name);
        if (service.Status == ServiceControllerStatus.Stopped)
            return;
        if (service.Status != ServiceControllerStatus.StopPending)
            service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, Timeout);
    });

    public static async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    public static ServiceStartupMode? GetStartupMode()
    {
        try
        {
            using var service = new ServiceController(Name);
            return service.StartType switch
            {
                ServiceStartMode.Automatic => IsDelayedAutoStart() ? ServiceStartupMode.AutomaticDelayed : ServiceStartupMode.Automatic,
                ServiceStartMode.Disabled => ServiceStartupMode.Disabled,
                _ => ServiceStartupMode.Manual,
            };
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static async Task SetStartupModeAsync(ServiceStartupMode mode)
    {
        var value = mode switch
        {
            ServiceStartupMode.AutomaticDelayed => "delayed-auto",
            ServiceStartupMode.Automatic => "auto",
            ServiceStartupMode.Manual => "demand",
            _ => "disabled",
        };
        var (exitCode, output) = await ProcessRunner.RunAsync("sc.exe", "config", Name, "start=", value);
        if (exitCode != 0)
            throw new InvalidOperationException($"變更啟動類型失敗（代碼 {exitCode}）：{output}");
    }

    private static bool IsDelayedAutoStart()
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{Name}");
        return key?.GetValue("DelayedAutostart") is int value && value != 0;
    }
}
