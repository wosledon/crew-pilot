using CrewPilot.Api.Contracts;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Mcp;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Mcp;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>MCP Server 管理：地址、传输方式、认证（加密）、启停与连通性测试。</summary>
[ApiController]
public sealed class McpServersController(
    CrewPilotDbContext db,
    ISecretProtector secrets,
    IHttpClientFactory httpClientFactory,
    AuditLogger audit) : ControllerBase
{
    private static readonly string[] Transports = ["http", "sse", "stdio"];

    [HttpGet("api/mcp-servers")]
    public async Task<ActionResult<IReadOnlyList<McpServerDto>>> List(CancellationToken ct)
    {
        var servers = await db.McpServers
            .AsNoTracking()
            .Include(s => s.Agents)
            .ToListAsync(ct);
        return Ok(servers
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList());
    }

    [HttpGet("api/mcp-servers/{id:guid}")]
    public async Task<ActionResult<McpServerDto>> Get(Guid id, CancellationToken ct)
    {
        var server = await db.McpServers
            .AsNoTracking()
            .Include(s => s.Agents)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        return server is null
            ? Problem(detail: "MCP Server 不存在", statusCode: 404)
            : Ok(ToDto(server));
    }

    [HttpPost("api/mcp-servers")]
    public async Task<ActionResult<McpServerDto>> Create(
        [FromBody] CreateMcpServerRequest request, CancellationToken ct)
    {
        var error = Validate(request.Name, request.Endpoint, request.Transport);
        if (error is not null)
        {
            return Problem(detail: error, statusCode: 400);
        }

        var server = new McpServer
        {
            Name = request.Name.Trim(),
            Endpoint = request.Endpoint.Trim(),
            Transport = NormalizeTransport(request.Transport),
            AuthTokenProtected = string.IsNullOrWhiteSpace(request.AuthToken)
                ? null
                : secrets.Protect(request.AuthToken.Trim()),
        };
        db.McpServers.Add(server);
        await db.SaveChangesAsync(ct);
        return Created($"/api/mcp-servers/{server.Id}", ToDto(server));
    }

    [HttpPut("api/mcp-servers/{id:guid}")]
    public async Task<ActionResult<McpServerDto>> Update(
        Guid id, [FromBody] UpdateMcpServerRequest request, CancellationToken ct)
    {
        var error = Validate(request.Name, request.Endpoint, request.Transport);
        if (error is not null)
        {
            return Problem(detail: error, statusCode: 400);
        }

        var server = await db.McpServers.Include(s => s.Agents).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (server is null)
        {
            return Problem(detail: "MCP Server 不存在", statusCode: 404);
        }

        server.Name = request.Name.Trim();
        server.Endpoint = request.Endpoint.Trim();
        server.Transport = NormalizeTransport(request.Transport);
        server.Enabled = request.Enabled;
        server.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.AuthToken))
        {
            server.AuthTokenProtected = secrets.Protect(request.AuthToken.Trim());
        }

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(server));
    }

    [HttpDelete("api/mcp-servers/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var server = await db.McpServers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (server is null)
        {
            return Problem(detail: "MCP Server 不存在", statusCode: 404);
        }

        db.McpServers.Remove(server);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("mcp.delete", "mcp", id.ToString(), ct: ct);
        return NoContent();
    }

    /// <summary>连通性测试：HTTP/SSE 探测端点；stdio 仅校验配置完整性。</summary>
    [HttpPost("api/mcp-servers/{id:guid}/test")]
    public async Task<ActionResult<McpTestDto>> Test(Guid id, CancellationToken ct)
    {
        var server = await db.McpServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (server is null)
        {
            return Problem(detail: "MCP Server 不存在", statusCode: 404);
        }

        if (string.Equals(server.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new McpTestDto(
                true, "stdio 配置已记录；运行时会启动该命令作为 MCP Server"));
        }

        if (string.Equals(server.Transport, "sse", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var token = string.IsNullOrWhiteSpace(server.AuthTokenProtected)
                    ? null
                    : secrets.Unprotect(server.AuthTokenProtected);
                await using var sse = await SseMcpClient.ConnectAsync(
                    server.Endpoint, token, TimeSpan.FromSeconds(10), ct);
                return Ok(new McpTestDto(
                    true, $"SSE 连接成功，发现 {sse.Tools.Count} 个工具"));
            }
            catch (Exception ex)
            {
                return Ok(new McpTestDto(false, $"SSE 连接失败：{ex.Message}"));
            }
        }

        try
        {
            using var client = httpClientFactory.CreateClient("mcp");
            client.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Get, server.Endpoint);
            if (!string.IsNullOrWhiteSpace(server.AuthTokenProtected))
            {
                try
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", secrets.Unprotect(server.AuthTokenProtected));
                }
                catch (Exception)
                {
                    return Ok(new McpTestDto(false, "认证 Token 解密失败，请重新配置"));
                }
            }

            using var response = await client.SendAsync(request, ct);
            var ok = (int)response.StatusCode is >= 200 and < 500;
            return Ok(new McpTestDto(
                ok,
                ok
                    ? $"端点可达（HTTP {(int)response.StatusCode}）"
                    : $"端点返回 HTTP {(int)response.StatusCode}"));
        }
        catch (Exception ex)
        {
            return Ok(new McpTestDto(false, $"端点不可达：{ex.Message}"));
        }
    }

    private static string? Validate(string name, string endpoint, string transport)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(endpoint))
        {
            return "名称与端点必填";
        }

        if (!Transports.Contains((transport ?? string.Empty).Trim().ToLowerInvariant()))
        {
            return "传输方式无效（http/sse/stdio）";
        }

        return null;
    }

    private static string NormalizeTransport(string transport) =>
        transport.Trim().ToLowerInvariant();

    private static McpServerDto ToDto(McpServer server) => new(
        server.Id,
        server.Name,
        server.Endpoint,
        server.Transport,
        !string.IsNullOrWhiteSpace(server.AuthTokenProtected),
        server.Enabled,
        server.Agents.Count,
        server.UpdatedAt);
}
