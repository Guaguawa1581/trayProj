using System.Reflection;

namespace BackendTray.Service.Management;

internal sealed class ServiceInfo
{
    public string Version { get; } =
        typeof(ServiceInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
}
