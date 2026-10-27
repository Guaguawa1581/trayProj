using Microsoft.Extensions.Hosting;

namespace BackendTray.Agent;

/// <summary>
/// 讀取 stdin：收到 "shutdown"，或 stdin 被關閉（代表服務已經結束），就停止應用程式。
/// Windows 無法對子程序送 Ctrl+C，所以用 stdin 當作關閉訊號。
/// </summary>
internal sealed class ShutdownListener(IHostApplicationLifetime lifetime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 用背景執行緒讀取：Console.In.ReadLine 無法取消，不能讓它拖住應用程式的關閉流程。
        var thread = new Thread(ReadLoop) { IsBackground = true, Name = "BackendTray shutdown listener" };
        thread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ReadLoop()
    {
        try
        {
            while (Console.In.ReadLine() is { } line)
            {
                if (string.Equals(line.Trim().Trim('﻿'), "shutdown", StringComparison.OrdinalIgnoreCase))
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }

        lifetime.StopApplication();
    }
}
