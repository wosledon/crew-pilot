using System.Security.Cryptography;
using System.Text;
using CrewPilot.Core.Abstractions;

namespace CrewPilot.Infrastructure.Security;

/// <summary>
/// 跨平台 AES-GCM 机密保护（Linux / Docker 使用）。
/// 密钥首次运行生成并持久化到本地文件；密文格式 v1:{base64(nonce|tag|cipher)}。
/// 密钥文件与数据库同属敏感数据，需随数据卷一起备份。
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const string Prefix = "v1:";

    private readonly byte[] _key;

    public AesGcmSecretProtector(string keyFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFilePath);
        _key = LoadOrCreateKey(keyFilePath);
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipher.CopyTo(payload, NonceSize + TagSize);
        return Prefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedText);
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不支持的机密格式（期望 v1: 前缀）");
        }

        var payload = Convert.FromBase64String(protectedText[Prefix.Length..]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new InvalidOperationException("机密数据不完整");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] LoadOrCreateKey(string keyFilePath)
    {
        var fullPath = Path.GetFullPath(keyFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(fullPath))
        {
            var existing = File.ReadAllBytes(fullPath);
            if (existing.Length != KeySize)
            {
                throw new InvalidOperationException(
                    $"密钥文件长度无效（期望 {KeySize} 字节）：{fullPath}");
            }

            return existing;
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(fullPath, key);
        // 尽力限制密钥文件权限（非 Unix 平台跳过）
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception)
            {
                // 权限不足时忽略（仍受目录权限保护）
            }
        }

        return key;
    }
}
