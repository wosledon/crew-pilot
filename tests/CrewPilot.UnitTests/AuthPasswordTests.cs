using CrewPilot.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public sealed class AuthPasswordTests
{
    [Fact]
    public void IsMatch_ReturnsFalseForWrongPassword()
    {
        AuthPassword.IsMatch("secret", "wrong").Should().BeFalse();
    }

    [Fact]
    public void IsMatch_ReturnsTrueForCorrectPassword()
    {
        AuthPassword.IsMatch("secret", "secret").Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenAuthDisabled_AlwaysTrue()
    {
        AuthPassword.IsMatch(null, "").Should().BeTrue();
        AuthPassword.IsMatch("", "anything").Should().BeTrue();
    }
}
