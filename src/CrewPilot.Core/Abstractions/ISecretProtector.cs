namespace CrewPilot.Core.Abstractions;

/// <summary>机密保护（DPAPI/AES-GCM），API key、token、MCP 认证入库前必须经此接口。</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedText);
}
