using CrewPilot.Agents.Execution;
using CrewPilot.Infrastructure.Providers;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public class AgentPlatformTests
{
    [Fact]
    public void FromDuration_ConvertsOneToOne()
    {
        AgentRunHours.FromDuration(TimeSpan.FromMinutes(90)).Should().Be(1.5m);
        AgentRunHours.FromDuration(TimeSpan.FromHours(2)).Should().Be(2m);
        AgentRunHours.FromDuration(TimeSpan.FromMinutes(7)).Should().Be(0.12m);
        AgentRunHours.FromDuration(TimeSpan.Zero).Should().Be(0m);
        AgentRunHours.FromDuration(TimeSpan.FromMinutes(-5)).Should().Be(0m);
    }

    [Fact]
    public void ParseCatalog_ExtractsModelsWithPricing()
    {
        const string json = """
        {
          "openai": {
            "name": "OpenAI",
            "models": {
              "gpt-4o-mini": {
                "name": "GPT-4o mini",
                "context": 128000,
                "capabilities": ["tools", "vision"],
                "pricing": { "input": 0.15, "output": 0.6 }
              }
            }
          },
          "deepseek": {
            "name": "DeepSeek",
            "models": {
              "deepseek-chat": { "name": "DeepSeek Chat", "context": 65536 }
            }
          }
        }
        """;

        var parsed = ModelsDevCatalogParser.Parse(json);

        parsed.Should().HaveCount(2);
        var gpt = parsed.Single(m => m.ModelId == "gpt-4o-mini");
        gpt.ProviderKey.Should().Be("openai");
        gpt.DisplayName.Should().Be("GPT-4o mini");
        gpt.ContextLength.Should().Be(128000);
        gpt.InputPricePerMillion.Should().Be(0.15m);
        gpt.OutputPricePerMillion.Should().Be(0.6m);
        gpt.Capabilities.Should().Contain("tools");

        var deepseek = parsed.Single(m => m.ProviderKey == "deepseek");
        deepseek.InputPricePerMillion.Should().BeNull();
    }

    [Fact]
    public void ParseCatalog_WithInvalidJsonShape_ReturnsEmpty()
    {
        ModelsDevCatalogParser.Parse("[]").Should().BeEmpty();
        ModelsDevCatalogParser.Parse("""{"openai": "not-an-object"}""").Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://api.deepseek.com/v1", "deepseek")]
    [InlineData("https://api.openai.com/v1", "openai")]
    [InlineData("https://dashscope.aliyuncs.com/compatible-mode/v1", "aliyuncs")]
    public void DeriveKey_ExtractsProviderKeyFromBaseUrl(string baseUrl, string expected)
    {
        ModelsDevSyncService.DeriveKey(baseUrl).Should().Be(expected);
    }
}
