namespace CrewPilot.Api.Auth;

/// <summary>认证配置：未配置密码时保持单用户开放（自托管默认）。</summary>
public sealed class AuthOptions
{
    public string? Password { get; set; }
}
