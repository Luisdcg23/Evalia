namespace EBR.Application.Identity;

public sealed record AuthenticationResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string Role,
    string FullName,
    string Email);
