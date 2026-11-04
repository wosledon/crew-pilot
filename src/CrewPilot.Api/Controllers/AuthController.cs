using System.Security.Claims;
using CrewPilot.Api.Auth;
using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CrewPilot.Api.Controllers;

/// <summary>单用户认证：密码登录 / 状态查询 / 退出。未配置 Auth:Password 时认证关闭。</summary>
[ApiController]
public sealed class AuthController(IOptions<AuthOptions> options) : ControllerBase
{
    [HttpGet("api/auth/status")]
    public ActionResult<AuthStatusDto> Status()
    {
        var enabled = !string.IsNullOrWhiteSpace(options.Value.Password);
        return Ok(new AuthStatusDto(
            enabled,
            enabled && (User.Identity?.IsAuthenticated ?? false)));
    }

    [HttpPost("api/auth/login")]
    public async Task<ActionResult<AuthStatusDto>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Password))
        {
            return Problem(detail: "认证未启用", statusCode: 400);
        }

        if (!AuthPassword.IsMatch(options.Value.Password, request.Password ?? string.Empty))
        {
            return Problem(detail: "密码错误", statusCode: 401);
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "owner")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Ok(new AuthStatusDto(true, true));
    }

    [HttpPost("api/auth/logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
