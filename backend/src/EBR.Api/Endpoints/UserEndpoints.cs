using System.Security.Claims;
using EBR.Domain.Identity;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users").WithTags("Usuarios").RequireAuthorization();
        group.MapGet("/me", GetCurrentAsync);
        group.MapGet("/pending", GetPendingAsync).RequireAuthorization(policy =>
            policy.RequireRole(SystemRoles.Administrator));
        group.MapPost("/{id:guid}/approve", ApproveAsync).RequireAuthorization(policy =>
            policy.RequireRole(SystemRoles.Administrator));
        group.MapPost("/{id:guid}/reject", RejectAsync).RequireAuthorization(policy =>
            policy.RequireRole(SystemRoles.Administrator));
        return endpoints;
    }

    private static async Task<IResult> GetCurrentAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = Guid.TryParse(identifier, out var userId)
            ? await userManager.FindByIdAsync(userId.ToString())
            : null;
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        // Empresas autorizadas: el join real contra Empresa_Usuario. Los roles que no se
        // asocian a una empresa concreta (ADMINISTRADOR, COORDINADOR, TECNICO_EVALUADOR)
        // simplemente no tienen filas en Empresa_Usuario, así que obtienen lista vacía sin
        // necesidad de una regla especial por rol.
        var authorizedCompanyIds = await context.CompanyUsers.AsNoTracking()
            .Where(link => link.UserId == user.Id)
            .OrderBy(link => link.CompanyId)
            .Select(link => link.CompanyId)
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            user.Id,
            Email = user.Email ?? string.Empty,
            user.FullName,
            Role = roles.Single(),
            AuthorizedCompanyIds = authorizedCompanyIds
        });
    }

    private static async Task<IResult> GetPendingAsync(UserManager<ApplicationUser> userManager)
    {
        var users = await userManager.Users
            .Where(user => user.ApprovalStatus == UserApprovalStatus.PendingValidation)
            .OrderBy(user => user.FullName)
            .Select(user => new
            {
                user.Id,
                Email = user.Email ?? string.Empty,
                user.FullName,
                user.DocumentNumber,
                user.PhoneNumber,
                user.RequestedRole
            })
            .ToListAsync();
        return Results.Ok(users);
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        if (user.ApprovalStatus != UserApprovalStatus.PendingValidation ||
            string.IsNullOrWhiteSpace(user.RequestedRole) ||
            !new[] { SystemRoles.CompanyAdministrator, SystemRoles.DelegateUser }.Contains(user.RequestedRole))
        {
            return Results.Conflict();
        }

        var roleResult = await userManager.AddToRoleAsync(user, user.RequestedRole);
        if (!roleResult.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = roleResult.Errors.Select(error => error.Description).ToArray()
            });
        }

        user.ApprovalStatus = UserApprovalStatus.Approved;
        var updateResult = await userManager.UpdateAsync(user);
        return updateResult.Succeeded ? Results.NoContent() : Results.Problem("No fue posible aprobar el usuario.");
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        RejectUserRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        if (user.ApprovalStatus != UserApprovalStatus.PendingValidation)
        {
            return Results.Conflict();
        }

        user.ApprovalStatus = UserApprovalStatus.Rejected;
        user.RejectionReason = request.Reason?.Trim();
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? Results.NoContent() : Results.Problem("No fue posible rechazar el usuario.");
    }

    private sealed record RejectUserRequest(string? Reason);
}
