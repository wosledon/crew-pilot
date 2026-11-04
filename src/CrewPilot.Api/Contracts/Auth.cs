namespace CrewPilot.Api.Contracts;

public sealed record AuthStatusDto(bool Enabled, bool Authenticated);

public sealed record LoginRequest(string Password);
