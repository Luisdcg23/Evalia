using System.Security.Claims;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class RegulatoryOriginEndpoints
{
    public static IEndpointRouteBuilder MapRegulatoryOriginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var alerts = endpoints.MapGroup("/api/alerts").WithTags("Alertas LAPCH")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Coordinator));
        alerts.MapGet("/", async (EbrDbContext db, CancellationToken ct) => Results.Ok(await db.HealthAlerts.AsNoTracking().OrderByDescending(x => x.ReceivedAt).ToListAsync(ct)));
        alerts.MapPost("/", CreateAlertAsync);
        alerts.MapPost("/{id:int}/decision", DecideAlertAsync);
        var complaints = endpoints.MapGroup("/api/complaints").WithTags("Denuncias")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Coordinator));
        complaints.MapGet("/", async (EbrDbContext db, CancellationToken ct) => Results.Ok(await db.Complaints.AsNoTracking().OrderByDescending(x => x.ReceivedAt).ToListAsync(ct)));
        complaints.MapPost("/", CreateComplaintAsync);
        complaints.MapPost("/{id:int}/decision", DecideComplaintAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAlertAsync(AlertRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        if (!await context.Companies.AnyAsync(item => item.Id == request.CompanyId && item.IsActive, cancellationToken)) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.AlertNumber) || string.IsNullOrWhiteSpace(request.Product) || string.IsNullOrWhiteSpace(request.Description)) return Invalid();
        var item = new HealthAlert { AlertNumber = request.AlertNumber.Trim(), ReceivedAt = request.ReceivedAt, Product = request.Product.Trim(), CompanyId = request.CompanyId, Description = request.Description.Trim(), CreatedBy = userId };
        context.HealthAlerts.Add(item); await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/alerts/{item.Id}", item);
    }

    private static async Task<IResult> CreateComplaintAsync(ComplaintRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.ComplaintType) || string.IsNullOrWhiteSpace(request.Complainant) || string.IsNullOrWhiteSpace(request.Description)) return Invalid();
        if (request.CompanyId.HasValue && !await context.Companies.AnyAsync(item => item.Id == request.CompanyId && item.IsActive, cancellationToken)) return Results.NotFound();
        var item = new Complaint { ComplaintType = request.ComplaintType.Trim(), ReceivedAt = request.ReceivedAt, Complainant = request.Complainant.Trim(), Description = request.Description.Trim(), CompanyId = request.CompanyId, CreatedBy = userId };
        context.Complaints.Add(item); await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/complaints/{item.Id}", item);
    }

    private static async Task<IResult> DecideAlertAsync(int id, DecisionRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        var item = await context.HealthAlerts.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        var result = request.Result.Trim().ToUpperInvariant();
        if (result is not ("PROCEED" or "NOT_PROCEED")) return Invalid();
        var inspectionCase = await context.InspectionCases.SingleOrDefaultAsync(value => value.SourceType == "ALERT" && value.SourceReferenceId == id, cancellationToken);
        if (item.Status != "PENDING") return Results.Ok(new { item.Status, CaseId = inspectionCase?.Id });
        item.Status = result; item.DecisionReason = request.Reason.Trim(); item.DecidedAt = DateTimeOffset.UtcNow;
        if (result == "PROCEED") inspectionCase = AddCase(context, item.CompanyId, "ALERT", item.Id, userId, "HIGH");
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { item.Status, CaseId = inspectionCase?.Id });
    }

    private static async Task<IResult> DecideComplaintAsync(int id, DecisionRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        var item = await context.Complaints.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        var result = request.Result.Trim().ToUpperInvariant();
        if (result is not ("PROCEED" or "NOT_PROCEED" or "REFERRED")) return Invalid();
        var inspectionCase = await context.InspectionCases.SingleOrDefaultAsync(value => value.SourceType == "COMPLAINT" && value.SourceReferenceId == id, cancellationToken);
        if (item.Status != "PENDING") return Results.Ok(new { item.Status, CaseId = inspectionCase?.Id });
        if (result == "PROCEED" && !item.CompanyId.HasValue) return Results.Conflict(new { message = "Debe asociar una empresa antes de generar la evaluación." });
        item.Status = result; item.DecisionReason = request.Reason.Trim(); item.DecidedAt = DateTimeOffset.UtcNow;
        if (result == "PROCEED") inspectionCase = AddCase(context, item.CompanyId!.Value, "COMPLAINT", item.Id, userId, "HIGH");
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { item.Status, CaseId = inspectionCase?.Id });
    }

    private static InspectionCase AddCase(EbrDbContext context, int companyId, string source, int referenceId, Guid userId, string priority)
    {
        var item = new InspectionCase { CompanyId = companyId, SourceType = source, SourceReferenceId = referenceId, CreatedBy = userId, Priority = priority };
        context.InspectionCases.Add(item); return item;
    }
    private static bool UserId(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out id);
    private static IResult Invalid() => Results.ValidationProblem(new Dictionary<string, string[]> { ["origin"] = ["Los datos o el resultado no son válidos."] });
    private sealed record AlertRequest(string AlertNumber, DateTimeOffset ReceivedAt, string Product, int CompanyId, string Description);
    private sealed record ComplaintRequest(string ComplaintType, DateTimeOffset ReceivedAt, string Complainant, string Description, int? CompanyId = null);
    private sealed record DecisionRequest(string Result, string Reason);
}
