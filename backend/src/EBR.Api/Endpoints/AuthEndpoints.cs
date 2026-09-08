using EBR.Application.Identity;
using EBR.Domain.Identity;
using EBR.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using EBR.Infrastructure.Persistence;

namespace EBR.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Autenticación").RequireRateLimiting("auth");
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        group.MapPost("/register", RegisterAsync).AllowAnonymous();
        group.MapPost("/forgot-password", ForgotPasswordAsync).AllowAnonymous();
        group.MapPost("/change-password", ChangePasswordAsync).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Correo y contraseña son obligatorios."]
            });
        }

        var result = await authenticationService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);
        return result is null ? Results.Unauthorized() : Results.Ok(result);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.Unauthorized();
        }

        var result = await authenticationService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result is null ? Results.Unauthorized() : Results.Ok(result);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var allowedRoles = new[] { SystemRoles.CompanyAdministrator, SystemRoles.DelegateUser };
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.DocumentNumber) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            !allowedRoles.Contains(request.RequestedRole, StringComparer.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["registration"] = ["Los datos de registro o el rol solicitado no son válidos."]
            });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            DocumentNumber = request.DocumentNumber.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            RequestedRole = request.RequestedRole,
            ApprovalStatus = UserApprovalStatus.PendingValidation
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["registration"] = result.Errors.Select(error => error.Description).ToArray()
            });
        }

        return Results.Accepted(value: new { user.Id, status = "PENDIENTE_VALIDACION" });
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        ClaimsPrincipal principal,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(identifier, out var userId) || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.Unauthorized();
        }

        await authenticationService.LogoutAsync(userId, request.RefreshToken, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        EbrDbContext context,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        string? recoveryCode = null;
        if (user is not null && user.ApprovalStatus == UserApprovalStatus.Approved)
        {
            var activeCodes = await context.PasswordRecoveryCodes
                .Where(code => code.UserId == user.Id && code.ConsumedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var activeCode in activeCodes)
            {
                activeCode.ConsumedAt = DateTimeOffset.UtcNow;
            }

            recoveryCode = RandomNumberGenerator.GetInt32(1000, 10000).ToString(CultureInfo.InvariantCulture);
            context.PasswordRecoveryCodes.Add(new PasswordRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CodeHash = HashRecoveryCode(user.Id, recoveryCode),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        return Results.Accepted(value: new
        {
            message = "Si la cuenta existe, se generó un código de recuperación.",
            recoveryCode = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? recoveryCode
                : null
        });
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        UserManager<ApplicationUser> userManager,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null)
        {
            return InvalidRecoveryCode();
        }

        var codeHash = HashRecoveryCode(user.Id, request.RecoveryCode.Trim());
        var recovery = await context.PasswordRecoveryCodes.SingleOrDefaultAsync(
            code => code.UserId == user.Id && code.CodeHash == codeHash,
            cancellationToken);
        if (recovery is null || recovery.ConsumedAt is not null || recovery.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return InvalidRecoveryCode();
        }

        var passwordErrors = new List<IdentityError>();
        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(userManager, user, request.NewPassword);
            if (!validation.Succeeded)
            {
                passwordErrors.AddRange(validation.Errors);
            }
        }

        if (passwordErrors.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newPassword"] = passwordErrors.Select(error => error.Description).ToArray()
            });
        }

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Results.Problem("No fue posible cambiar la contraseña.");
        }

        recovery.ConsumedAt = DateTimeOffset.UtcNow;
        var refreshTokens = await context.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult InvalidRecoveryCode() => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["recoveryCode"] = ["El código no es válido o expiró."] });

    private static string HashRecoveryCode(Guid userId, string code) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{code}")));

    private sealed record LoginRequest(string Email, string Password);

    private sealed record RefreshRequest(string RefreshToken);

    private sealed record LogoutRequest(string RefreshToken);

    private sealed record ForgotPasswordRequest(string Email);

    private sealed record ChangePasswordRequest(string Email, string RecoveryCode, string NewPassword);

    private sealed record RegisterRequest(
        string FullName,
        string DocumentNumber,
        string? PhoneNumber,
        string Email,
        string Password,
        string RequestedRole);
}
