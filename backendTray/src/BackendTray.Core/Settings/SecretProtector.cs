using System.Security.Cryptography;
using System.Text;

namespace BackendTray.Core.Settings;

/// <summary>
/// 以 DPAPI（LocalMachine 範圍）加密密碼：同一台電腦的任何帳戶都能解密，換電腦則不行。
/// 設定檔所在的資料夾另外以 ACL 限制只有 SYSTEM、Administrators 與服務帳戶能存取。
/// </summary>
public sealed class SecretProtector
{
    private static readonly byte[] Entropy = "BackendTray.Settings.v1"u8.ToArray();

    public string Protect(string plainText)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedText)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(bytes);
    }
}
