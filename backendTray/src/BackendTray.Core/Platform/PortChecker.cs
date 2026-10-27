using System.Net.NetworkInformation;

namespace BackendTray.Core.Platform;

public static class PortChecker
{
    /// <summary>目前是否有任何程式在此 TCP port 上監聽。</summary>
    public static bool IsListening(int port) =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);
}
