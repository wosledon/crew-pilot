using System.Net;
using System.Text;
using System.Text.Json;
using CrewPilot.Infrastructure.Mcp;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public sealed class McpClientTests
{
    [Fact]
    public async Task Connect_ListsTools_And_CallsTool()
    {
        var handler = new FakeMcpHandler();
        using var http = new HttpClient(handler);
        await using var client = await McpClient.ConnectAsync(
            http, "https://mcp.example.com/mcp", "test-token");

        client.Tools.Should().ContainSingle();
        client.Tools[0].Name.Should().Be("echo");
        client.Tools[0].InputSchemaJson.Should().Contain("text");

        var result = await client.CallToolAsync("echo", """{"text":"hi"}""");
        result.Should().Be("hello world");

        var requests = handler.Requests;
        requests.Should().Contain(r => r.Contains("\"initialize\""));
        requests.Should().Contain(r => r.Contains("\"notifications/initialized\""));
        requests.Should().Contain(r => r.Contains("\"tools/list\""));
        requests.Should().Contain(r => r.Contains("\"tools/call\""));
        requests.Should().Contain(r => r.Contains("\"test-token\"") == false);
        handler.AuthorizationHeaders.Should().OnlyContain(h => h == "Bearer test-token");
    }

    [Fact]
    public async Task CallTool_WithMalformedArguments_Throws()
    {
        var handler = new FakeMcpHandler();
        using var http = new HttpClient(handler);
        await using var client = await McpClient.ConnectAsync(
            http, "https://mcp.example.com/mcp");

        await FluentActions.Awaiting(() => client.CallToolAsync("echo", "{not-json"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class FakeMcpHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> AuthorizationHeaders { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(body);
            if (request.Headers.Authorization is { } auth)
            {
                AuthorizationHeaders.Add($"{auth.Scheme} {auth.Parameter}");
            }

            using var document = JsonDocument.Parse(body);
            var method = document.RootElement.TryGetProperty("method", out var methodElement)
                ? methodElement.GetString()
                : null;

            return method switch
            {
                "initialize" => Json(
                    """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"fake","version":"1"}}}"""),
                "notifications/initialized" => new HttpResponseMessage(HttpStatusCode.Accepted),
                "tools/list" => Json(
                    """{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"echo","description":"回显","inputSchema":{"type":"object","properties":{"text":{"type":"string"}}}}]}}"""),
                "tools/call" => Json(
                    """{"jsonrpc":"2.0","id":3,"result":{"content":[{"type":"text","text":"hello world"}]}}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}
