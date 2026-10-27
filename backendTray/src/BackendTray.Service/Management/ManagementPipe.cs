using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace BackendTray.Service.Management;

internal static class ManagementPipe
{
    /// <summary>
    /// 只允許 SYSTEM、Administrators 與服務自己的帳戶連線。
    /// 一定要包含服務自己的帳戶，否則 Kestrel 無法建立後續的 pipe instance。
    /// </summary>
    public static PipeSecurity CreateSecurity()
    {
        var security = new PipeSecurity();
        void Allow(SecurityIdentifier sid) =>
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));

        Allow(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        Allow(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        using var current = WindowsIdentity.GetCurrent();
        Allow(current.User!);
        return security;
    }
}
