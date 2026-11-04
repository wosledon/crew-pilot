using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace CrewPilot.Api.Auth;

/// <summary>
/// 认证门禁：启用密码后，除登录/状态/健康检查/OpenAPI 外的所有 /api 请求都要求 Cookie 登录。
/// 未配置密码时直接放行（单用户自托管默认体验）。
/// </summary>
public sealed class AuthGateMiddleware(
    RequestDelegate next, IOptions<AuthOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Password) || IsPublic(context.Request.Path))
        {
            await next(context);
            return;
        }

        var result = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        if (result.Succeeded)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            title = "未登录",
            status = 401,
            detail = "请先登录",
        });
    }

    private static bool IsPublic(PathString path) =>
        path.StartsWithSegments("/api/auth") ||
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/openapi");
}
