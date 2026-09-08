using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using EBR.Application.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EBR.Infrastructure.Identity;

public sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    EbrDbContext context,
    IOptions<JwtOptions> jwtOptions) : IAuthenticationService
{
    public async Task<AuthenticationResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || user.ApprovalStatus != UserApprovalStatus.Approved)
        {
            return null;
        }

        if (await userManager.IsLockedOutAsync(user) ||
            !await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthenticationResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await context.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        if (storedToken is null || storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (storedToken.RevokedAt is not null)
        {
            var activeTokens = await context.RefreshTokens
                .Where(token => token.UserId == storedToken.UserId && token.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var activeToken in activeTokens)
            {
                activeToken.RevokedAt = DateTimeOffset.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
            return null;
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user is null || user.ApprovalStatus != UserApprovalStatus.Approved)
        {
            return null;
        }

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        var session = await CreateSessionAsync(user, cancellationToken);
        storedToken.ReplacedByHash = HashToken(session.RefreshToken);
        await context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<bool> LogoutAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await context.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash && token.UserId == userId,
            cancellationToken);
        if (storedToken is null || storedToken.RevokedAt is not null)
        {
            return false;
        }

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AuthenticationResult> CreateSessionAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Single();
        var options = jwtOptions.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var plainRefreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(plainRefreshToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(options.RefreshTokenDays)
        });
        await context.SaveChangesAsync(cancellationToken);

        return new AuthenticationResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            plainRefreshToken,
            expiresAt,
            role,
            user.FullName,
            user.Email ?? user.UserName ?? string.Empty);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
