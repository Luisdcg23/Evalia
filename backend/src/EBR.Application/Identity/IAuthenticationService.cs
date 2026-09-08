namespace EBR.Application.Identity;

public interface IAuthenticationService
{
    Task<AuthenticationResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<AuthenticationResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task<bool> LogoutAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken);
}
