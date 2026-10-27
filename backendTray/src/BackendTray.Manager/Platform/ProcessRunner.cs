using System.Diagnostics;

namespace BackendTray.Manager.Platform;

internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output)> RunAsync(string fileName, params string[] arguments)
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
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, (await output + await error).Trim());
    }

    /// <summary>
    /// 用檔案總管開啟網址或資料夾。管理介面以系統管理員身分執行，透過 explorer 開啟可避免瀏覽器也以管理員身分執行。
    /// </summary>
    public static void OpenInShell(string target)
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add(target);
        Process.Start(startInfo)?.Dispose();
    }
}
