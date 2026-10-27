using BackendTray.Contracts;
using BackendTray.Core.Database;
using BackendTray.Core.Platform;
using BackendTray.Core.Servers;
using Microsoft.Extensions.Logging;

namespace BackendTray.Core.Settings;

/// <summary>管理 API 的設定功能：讀取、驗證後儲存並套用、測試資料庫連線。</summary>
public sealed class SettingsService(
    SettingsStore store,
    SettingsMapper mapper,
    ServerCatalog catalog,
    ServerSupervisor supervisor,
    ConnectionTester tester,
    ILogger<SettingsService> logger)
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SettingsDto Get() => mapper.ToDto(store.Load());

    public async Task<SaveSettingsResult> SaveAsync(SettingsDto dto, CancellationToken cancellationToken)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var current = store.Load();
            if (dto.Revision != current.Revision)
            {
                return new SaveSettingsResult
                {
                    Outcome = SaveOutcome.Conflict,
                    Revision = current.Revision,
                    Errors = ["設定已被其他管理員修改，請重新載入後再儲存。"],
                };
            }

            var proposed = mapper.Apply(dto, current);
            var errors = SettingsValidator.Validate(proposed, current, catalog.Servers, supervisor.ActivePorts, PortChecker.IsListening);
            if (errors.Count > 0)
                return new SaveSettingsResult { Outcome = SaveOutcome.Invalid, Revision = current.Revision, Errors = errors };

            var saved = store.Save(proposed);
            logger.LogInformation("設定已儲存（revision {Revision}）", saved.Revision);

            // 套用期間不因呼叫端斷線而中斷，避免只重啟了一半。
            var restarted = await supervisor.ApplySettingsAsync(saved, CancellationToken.None);
            return new SaveSettingsResult { Outcome = SaveOutcome.Saved, Revision = saved.Revision, RestartedServers = [.. restarted] };
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public Task<DbTestResult> TestConnectionAsync(DbTestRequest request, CancellationToken cancellationToken)
    {
        var password = !string.IsNullOrEmpty(request.Connection.NewPassword)
            ? request.Connection.NewPassword
            : mapper.GetStoredPassword(request.StoredPasswordSlot, store.Load()) ?? "";
        var settings = mapper.FromDto(request.Connection, storedProtectedPassword: null);
        return tester.TestAsync(settings, password, cancellationToken);
    }
}
