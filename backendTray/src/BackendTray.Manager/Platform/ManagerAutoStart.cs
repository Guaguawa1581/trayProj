using System.Xml.Linq;
using BackendTray.Contracts;

namespace BackendTray.Manager.Platform;

/// <summary>「登入時開啟管理介面」的工作排程器工作（由安裝程式建立，這裡只切換啟用狀態）。</summary>
internal static class ManagerAutoStart
{
    /// <summary>工作不存在時回傳 null。</summary>
    public static async Task<bool?> IsEnabledAsync()
    {
        var (exitCode, output) = await ProcessRunner.RunAsync("schtasks.exe", "/Query", "/TN", ManagementConstants.ManagerTaskName, "/XML");
        if (exitCode != 0)
            return null;

        var document = XDocument.Parse(output);
        var ns = document.Root!.Name.Namespace;
        var enabled = document.Root.Element(ns + "Settings")?.Element(ns + "Enabled")?.Value;
        return !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task SetEnabledAsync(bool enabled)
    {
        var (exitCode, output) = await ProcessRunner.RunAsync(
            "schtasks.exe", "/Change", "/TN", ManagementConstants.ManagerTaskName, enabled ? "/ENABLE" : "/DISABLE");
        if (exitCode != 0)
            throw new InvalidOperationException($"變更排程工作失敗（代碼 {exitCode}）：{output}");
    }
}
