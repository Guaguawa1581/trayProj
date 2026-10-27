using BackendTray.Contracts;
using BackendTray.Core;
using BackendTray.Core.Servers;
using BackendTray.Core.Settings;

namespace BackendTray.Service.Management;

/// <summary>管理介面透過 named pipe 呼叫的 API。</summary>
internal static class ManagementEndpoints
{
    public static void MapManagementApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/status", (ServerSupervisor supervisor, ServiceInfo info, AppPaths paths) => new ServiceStatusDto
        {
            Version = info.Version,
            StartedAt = info.StartedAt,
            Paused = supervisor.IsPaused,
            InstallRoot = paths.InstallRoot,
            DataRoot = paths.DataRoot,
            LogsDirectory = paths.LogsDirectory,
            Servers = [.. supervisor.GetStatuses()],
            Warnings = [.. supervisor.Warnings],
        });

        // 會改變狀態的操作不因呼叫端斷線而中斷，避免只停了一半。
        app.MapPost("/servers/pause", async (ServerSupervisor supervisor) =>
        {
            await supervisor.PauseAsync(CancellationToken.None);
            return Results.NoContent();
        });

        app.MapPost("/servers/resume", async (ServerSupervisor supervisor) =>
        {
            await supervisor.ResumeAsync(CancellationToken.None);
            return Results.NoContent();
        });

        app.MapPost("/servers/{id}/restart", async (string id, ServerSupervisor supervisor) =>
            await supervisor.RestartAsync(id, CancellationToken.None) switch
            {
                RestartResult.Restarted => Results.NoContent(),
                RestartResult.NotFound => Results.NotFound(),
                _ => Results.Conflict("服務已暫停，請先繼續。"),
            });

        app.MapGet("/logs/{id}", (string id, int? tail, ServerSupervisor supervisor) =>
            supervisor.GetRecentLines(id, tail ?? 300) is { } lines
                ? Results.Ok(new ServerLogDto { ServerId = id, Lines = [.. lines] })
                : Results.NotFound());

        app.MapGet("/settings", (SettingsService settings) => settings.Get());

        app.MapPut("/settings", async (SettingsDto dto, SettingsService settings, CancellationToken cancellationToken) =>
        {
            var result = await settings.SaveAsync(dto, cancellationToken);
            return result.Outcome switch
            {
                SaveOutcome.Saved => Results.Ok(result),
                SaveOutcome.Conflict => Results.Conflict(result),
                _ => Results.BadRequest(result),
            };
        });

        app.MapPost("/database/test", (DbTestRequest request, SettingsService settings, CancellationToken cancellationToken) =>
            settings.TestConnectionAsync(request, cancellationToken));
    }
}
