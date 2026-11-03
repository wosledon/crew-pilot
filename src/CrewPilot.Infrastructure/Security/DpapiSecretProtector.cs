using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using CrewPilot.Core.Abstractions;

namespace CrewPilot.Infrastructure.Security;

/// <summary>
/// 基于 Windows DPAPI（CurrentUser 作用域）的机密保护实现。
/// API key、token、MCP 认证入库前必须经此接口加密；永不出现在响应与日志。
/// </summary>
[SupportedOSPlatform("windows")]
public class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CrewPilot:v1");

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var cipher = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    public string Unprotect(string protectedText)
    {
        ArgumentNullException.ThrowIfNull(protectedText);
        var cipher = Convert.FromBase64String(protectedText);
        var data = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }
}
