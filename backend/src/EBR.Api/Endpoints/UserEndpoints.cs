using System.Security.Claims;
using EBR.Application.Email;
using EBR.Domain.Identity;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static partial class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users").WithTags("Usuarios").RequireAuthorization();
        group.MapGet("/me", GetCurrentAsync);
        group.MapGet("/pending", GetPendingAsync).RequireAuthorization(policy =>
            policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/{id:guid}/registration-documents", GetRegistrationDocumentsAsync).RequireAuthorization(policy =>
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

    private static async Task<IResult> GetPendingAsync(
        UserManager<ApplicationUser> userManager,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .Where(user => user.ApprovalStatus == UserApprovalStatus.PendingValidation)
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);
        var userIds = users.Select(user => user.Id).ToList();
        var documents = await context.UserRegistrationDocuments.AsNoTracking()
            .Where(document => userIds.Contains(document.UserId))
            .ToListAsync(cancellationToken);

        return Results.Ok(users.Select(user => new
        {
            user.Id,
            Email = user.Email ?? string.Empty,
            user.FullName,
            user.DocumentNumber,
            user.PhoneNumber,
            user.RequestedRole,
            RegistrationDocuments = documents.Where(document => document.UserId == user.Id).ToList()
        }));
    }

    private static async Task<IResult> GetRegistrationDocumentsAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var documents = await context.UserRegistrationDocuments.AsNoTracking()
            .Where(document => document.UserId == id)
            .OrderBy(document => document.UploadedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(documents);
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
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
        if (!updateResult.Succeeded)
        {
            return Results.Problem("No fue posible aprobar el usuario.");
        }

        var content = EmailTemplates.RegistrationApproved(user.FullName, configuration["Frontend:Origin"]);
        await SendDecisionEmailAsync(emailSender, logger, user.Email, content, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        RejectUserRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
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

        var reason = request.Reason?.Trim();
        user.ApprovalStatus = UserApprovalStatus.Rejected;
        user.RejectionReason = reason;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Results.Problem("No fue posible rechazar el usuario.");
        }

        var content = EmailTemplates.RegistrationRejected(user.FullName, reason);
        await SendDecisionEmailAsync(emailSender, logger, user.Email, content, cancellationToken);
        return Results.NoContent();
    }

    // El correo es un complemento del aviso de aprobación/rechazo, nunca un requisito: un proveedor
    // SMTP caído no puede bloquear la decisión del administrador sobre un registro pendiente.
    private static async Task SendDecisionEmailAsync(
        IEmailSender emailSender, ILogger<Program> logger, string? toEmail, EmailTemplates.EmailContent content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;
        try
        {
            await emailSender.SendAsync(toEmail, content.Subject, content.PlainText, content.Html, cancellationToken);
        }
        catch (Exception ex)
        {
            LogRegistrationDecisionEmailFailed(logger, toEmail, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible enviar el correo de decisión de registro a {ToEmail}.")]
    private static partial void LogRegistrationDecisionEmailFailed(ILogger logger, string toEmail, Exception ex);

    private sealed record RejectUserRequest(string? Reason);
}
