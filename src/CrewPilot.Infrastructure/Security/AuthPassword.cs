namespace CrewPilot.Infrastructure.Security;

/// <summary>单用户密码比较：常量时间，避免计时侧信道。</summary>
public static class AuthPassword
{
    public static bool IsMatch(string? expected, string provided)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided ?? string.Empty);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            expectedBytes, providedBytes);
    }
}
