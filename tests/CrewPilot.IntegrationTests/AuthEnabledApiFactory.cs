using Microsoft.AspNetCore.Hosting;

namespace CrewPilot.IntegrationTests;

/// <summary>启用密码认证的测试工厂（覆盖 Auth:Password 配置）。</summary>
public sealed class AuthEnabledApiFactory : CrewPilotApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:Password", "test-password");
    }
}
