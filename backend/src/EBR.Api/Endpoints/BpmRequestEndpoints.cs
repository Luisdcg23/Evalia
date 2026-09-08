using System.Security.Claims;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class BpmRequestEndpoints
{
    public static IEndpointRouteBuilder MapBpmRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/bpm-requests").WithTags("Solicitudes BPM").RequireAuthorization();
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(policy => policy.RequireRole(
            SystemRoles.Administrator, SystemRoles.CompanyAdministrator, SystemRoles.DelegateUser));
        group.MapPut("/{id:int}", UpdateAsync).RequireAuthorization(policy => policy.RequireRole(
            SystemRoles.Administrator, SystemRoles.CompanyAdministrator, SystemRoles.DelegateUser));
        group.MapPost("/{id:int}/submit", SubmitAsync).RequireAuthorization(policy => policy.RequireRole(
            SystemRoles.Administrator, SystemRoles.CompanyAdministrator, SystemRoles.DelegateUser));
        return endpoints;
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        var query = context.BpmRequests.AsNoTracking();
        if (IsCompanyUser(principal))
        {
            if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
            query = query.Where(item => context.CompanyUsers.Any(link => link.CompanyId == item.CompanyId && link.UserId == userId));
        }
        return Results.Ok(await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken));
    }

    private static async Task<IResult> CreateAsync(CreateRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        if (!await CanAccessCompanyAsync(request.CompanyId, userId, principal, context, cancellationToken)) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.EstablishmentType) || string.IsNullOrWhiteSpace(request.Reason)) return Validation();
        var item = new BpmRequest
        {
            CompanyId = request.CompanyId,
            EstablishmentType = request.EstablishmentType.Trim(),
            Reason = request.Reason.Trim(),
            Observations = request.Observations?.Trim() ?? "",
            CreatedBy = userId
        };
        context.BpmRequests.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/bpm-requests/{item.Id}", item);
    }

    private static async Task<IResult> UpdateAsync(int id, CreateRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var item = await context.BpmRequests.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null || !await CanAccessCompanyAsync(item.CompanyId, userId, principal, context, cancellationToken)) return Results.NotFound();
        if (item.Status != BpmRequestStatuses.Draft) return Results.Conflict(new { message = "Una solicitud enviada ya no puede editarse." });
        if (request.CompanyId != item.CompanyId || string.IsNullOrWhiteSpace(request.EstablishmentType) || string.IsNullOrWhiteSpace(request.Reason)) return Validation();
        item.EstablishmentType = request.EstablishmentType.Trim();
        item.Reason = request.Reason.Trim();
        item.Observations = request.Observations?.Trim() ?? "";
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.VersionToken = Guid.NewGuid();
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private static async Task<IResult> SubmitAsync(int id, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var request = await context.BpmRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (request is null || !await CanAccessCompanyAsync(request.CompanyId, userId, principal, context, cancellationToken)) return Results.NotFound();
        var existing = await context.InspectionCases.SingleOrDefaultAsync(
            item => item.SourceType == "BPM_REQUEST" && item.SourceReferenceId == id, cancellationToken);
        if (existing is not null) return Results.Ok(existing);
        if (request.Status != BpmRequestStatuses.Draft) return Results.Conflict(new { message = "La solicitud no puede enviarse desde su estado actual." });
        var now = DateTimeOffset.UtcNow;
        request.Status = BpmRequestStatuses.Submitted;
        request.SubmittedAt = now;
        request.UpdatedAt = now;
        request.VersionToken = Guid.NewGuid();
        var inspectionCase = new InspectionCase
        {
            CompanyId = request.CompanyId,
            SourceType = "BPM_REQUEST",
            SourceReferenceId = request.Id,
            CreatedBy = userId
        };
        context.InspectionCases.Add(inspectionCase);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(inspectionCase);
    }

    private static bool IsCompanyUser(ClaimsPrincipal principal) =>
        principal.IsInRole(SystemRoles.CompanyAdministrator) || principal.IsInRole(SystemRoles.DelegateUser);

    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static async Task<bool> CanAccessCompanyAsync(int companyId, Guid userId, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken) =>
        principal.IsInRole(SystemRoles.Administrator)
            ? await context.Companies.AnyAsync(item => item.Id == companyId && item.IsActive, cancellationToken)
            : await context.CompanyUsers.AnyAsync(item => item.CompanyId == companyId && item.UserId == userId, cancellationToken);

    private static IResult Validation() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["request"] = ["Empresa, tipo de establecimiento y motivo son obligatorios."]
    });

    private sealed record CreateRequest(int CompanyId, string EstablishmentType, string Reason, string? Observations);
}
