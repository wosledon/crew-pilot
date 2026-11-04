using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using CrewPilot.Infrastructure.Mcp;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public sealed class StdioMcpClientTests
{
    [Fact]
    public async Task Connect_And_CallTool_OverLineProtocol()
    {
        // 双向管道：client stdin -> server reader；server writer -> client stdout
        var toServer = new Pipe();
        var fromServer = new Pipe();
        await using var clientStdin = new StreamWriter(toServer.Writer.AsStream(), Encoding.UTF8);
        using var clientStdout = new StreamReader(fromServer.Reader.AsStream(), Encoding.UTF8);
        using var serverReader = new StreamReader(toServer.Reader.AsStream(), Encoding.UTF8);
        await using var serverWriter = new StreamWriter(fromServer.Writer.AsStream(), Encoding.UTF8);

        var responder = Task.Run(async () =>
        {
            while (await serverReader.ReadLineAsync() is { } line)
            {
                using var document = JsonDocument.Parse(line);
                if (!document.RootElement.TryGetProperty("id", out var idElement))
                {
                    continue; // notification
                }

                var method = document.RootElement.GetProperty("method").GetString();
                var id = idElement.GetInt32();
                var result = method switch
                {
                    "initialize" =>
                        """{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"fake","version":"1"}}""",
                    "tools/list" =>
                        """{"tools":[{"name":"echo","description":"回显","inputSchema":{"type":"object"}}]}""",
                    "tools/call" =>
                        """{"content":[{"type":"text","text":"stdio hello"}]}""",
                    _ => "{}",
                };
                await serverWriter.WriteLineAsync(
                    $$"""{"jsonrpc":"2.0","id":{{id}},"result":{{result}}}""");
                await serverWriter.FlushAsync();
            }
        });

        await using var client = await StdioMcpClient.ConnectAsync(
            clientStdout, clientStdin);

        client.Tools.Should().ContainSingle();
        client.Tools[0].Name.Should().Be("echo");
        (await client.CallToolAsync("echo", """{"text":"hi"}"""))
            .Should().Be("stdio hello");

        await client.DisposeAsync();
        await responder.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Tokenize_SupportsQuotedArguments()
    {
        var tokens = StdioMcpClient.Tokenize(
            "npx -y @modelcontextprotocol/server-filesystem \"/path with space\"");

        tokens.Should().Equal(
            "npx", "-y", "@modelcontextprotocol/server-filesystem", "/path with space");
    }
}
