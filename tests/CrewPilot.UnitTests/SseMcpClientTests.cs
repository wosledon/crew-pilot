using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrewPilot.Infrastructure.Mcp;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public sealed class SseMcpClientTests
{
    [Fact]
    public async Task Connect_And_CallTool_OverLegacySseTransport()
    {
        using var handler = new FakeSseHandler();
        using var http = new HttpClient(handler);
        await using var client = await SseMcpClient.ConnectAsync(
            http, "http://mcp.test/sse", "secret", TimeSpan.FromSeconds(5));

        client.Tools.Should().ContainSingle();
        client.Tools[0].Name.Should().Be("echo");
        (await client.CallToolAsync("echo", """{"text":"hi"}"""))
            .Should().Be("legacy hello");
        handler.LastMessageUri!.AbsolutePath.Should().Be("/messages");
        handler.LastMessageUri.Query.Should().Contain("sessionId=abc");
        handler.AuthorizationHeader.Should().Be("Bearer secret");
    }

    /// <summary>假 MCP 旧版 SSE 服务端：GET 返回事件流，POST /messages 接收请求并把响应写回事件流。</summary>
    private sealed class FakeSseHandler : HttpMessageHandler
    {
        private readonly Pipe _pipe = new();
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public FakeSseHandler()
        {
            _writer = new StreamWriter(_pipe.Writer.AsStream(), new UTF8Encoding(false))
            {
                AutoFlush = true,
            };
        }

        public Uri? LastMessageUri { get; private set; }

        public string? AuthorizationHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            AuthorizationHeader = request.Headers.Authorization?.ToString();

            if (request.Method == HttpMethod.Get)
            {
                await WriteEventAsync("endpoint", "/messages?sessionId=abc", ct);
                var content = new StreamContent(_pipe.Reader.AsStream());
                content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }

            LastMessageUri = request.RequestUri;
            var body = await request.Content!.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var idElement))
            {
                var method = root.GetProperty("method").GetString();
                var result = method switch
                {
                    "initialize" =>
                        """{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"legacy","version":"1"}}""",
                    "tools/list" =>
                        """{"tools":[{"name":"echo","description":"回显","inputSchema":{"type":"object"}}]}""",
                    "tools/call" =>
                        """{"content":[{"type":"text","text":"legacy hello"}]}""",
                    _ => "{}",
                };
                await WriteEventAsync(
                    "message",
                    $$"""{"jsonrpc":"2.0","id":{{idElement.GetInt32()}},"result":{{result}}}""",
                    ct);
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        private async Task WriteEventAsync(string eventName, string data, CancellationToken ct)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                await _writer.WriteAsync($"event: {eventName}\n");
                await _writer.WriteAsync($"data: {data}\n\n");
                await _writer.FlushAsync(ct);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _writer.Dispose();
                _writeLock.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
