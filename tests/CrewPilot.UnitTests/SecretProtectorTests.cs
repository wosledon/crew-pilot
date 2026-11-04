using CrewPilot.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public sealed class SecretProtectorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "crewpilot_secret_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ProtectAndUnprotect_RoundTrips()
    {
        Directory.CreateDirectory(_directory);
        var protector = new AesGcmSecretProtector(Path.Combine(_directory, "secrets.key"));

        var cipher = protector.Protect("sk-test-123");

        cipher.Should().StartWith("v1:");
        cipher.Should().NotContain("sk-test-123");
        protector.Unprotect(cipher).Should().Be("sk-test-123");
    }

    [Fact]
    public void KeyFile_ReusedAcrossInstances()
    {
        Directory.CreateDirectory(_directory);
        var keyPath = Path.Combine(_directory, "secrets.key");
        var first = new AesGcmSecretProtector(keyPath);
        var cipher = first.Protect("token");

        var second = new AesGcmSecretProtector(keyPath);
        second.Unprotect(cipher).Should().Be("token");
        File.Exists(keyPath).Should().BeTrue();
        new FileInfo(keyPath).Length.Should().Be(32);
    }

    [Fact]
    public void TamperedCiphertext_Throws()
    {
        Directory.CreateDirectory(_directory);
        var protector = new AesGcmSecretProtector(Path.Combine(_directory, "secrets.key"));
        var cipher = protector.Protect("secret");
        var tampered = cipher[..^2] + "AA";

        FluentActions.Invoking(() => protector.Unprotect(tampered))
            .Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // 清理失败不影响测试结果
        }
    }
}
