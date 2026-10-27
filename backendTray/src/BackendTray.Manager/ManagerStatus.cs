using System.ServiceProcess;
using BackendTray.Contracts;

namespace BackendTray.Manager;

internal enum TrayState
{
    Unknown,
    NotInstalled,
    Stopped,
    Transitioning,
    Unreachable,
    AccessDenied,
    Paused,
    Starting,
    Running,
    Warning,
    Error,
}

/// <summary>綜合 SCM 狀態與管理 API 回應得到的整體狀態，決定圖示、提示文字與可用的操作。</summary>
internal sealed record ManagerStatus(TrayState State, ServiceControllerStatus? ScmStatus, ServiceStatusDto? Service, string? Error)
{
    public static ManagerStatus Initial { get; } = new(TrayState.Unknown, null, null, null);

    public bool IsInstalled => ScmStatus is not null;

    public bool IsReachable => Service is not null;

    public string Summary => State switch
    {
        TrayState.Unknown => "正在讀取狀態…",
        TrayState.NotInstalled => "服務未安裝",
        TrayState.Stopped => "服務已停止",
        TrayState.Transitioning => "服務正在啟動或停止…",
        TrayState.Unreachable => "服務執行中，但目前無法連線（可能正在啟動）",
        TrayState.AccessDenied => "權限不足，請以系統管理員身分執行管理介面",
        TrayState.Paused => "已暫停：所有網站與 API 都已停止",
        TrayState.Starting => "伺服器啟動中…",
        TrayState.Running => "執行中",
        TrayState.Warning => "執行中（有警告）",
        TrayState.Error => "有伺服器發生異常",
        _ => State.ToString(),
    };

    public static ManagerStatus FromService(ServiceControllerStatus? scmStatus, ServiceStatusDto service)
    {
        var active = service.Servers.Where(s => s.State != ServerState.Disabled).ToList();
        var state =
            service.Paused ? TrayState.Paused :
            active.Any(s => s.State is ServerState.Faulted or ServerState.Unhealthy) ? TrayState.Error :
            active.Any(s => s.State is ServerState.Starting or ServerState.Stopping or ServerState.Stopped) ? TrayState.Starting :
            service.Warnings.Count > 0 ? TrayState.Warning :
            TrayState.Running;
        return new ManagerStatus(state, scmStatus, service, null);
    }

    public static ManagerStatus FromFailure(ServiceControllerStatus? scmStatus, ServiceClientException exception)
    {
        if (exception.Error == ServiceClientError.AccessDenied)
            return new ManagerStatus(TrayState.AccessDenied, scmStatus, null, exception.Message);

        var state = scmStatus switch
        {
            null => TrayState.NotInstalled,
            ServiceControllerStatus.Stopped => TrayState.Stopped,
            ServiceControllerStatus.Running => TrayState.Unreachable,
            _ => TrayState.Transitioning,
        };
        return new ManagerStatus(state, scmStatus, null, exception.Message);
    }
}
