using System.Security.Claims;
using EBR.Domain.Evaluations;
using EBR.Domain.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

/// <summary>
/// Informe de evaluación versionado (fase 5.1, RF-16). Cada emisión crea una versión nueva y las
/// anteriores se conservan.
/// </summary>
public static class EvaluationReportEndpoints
{
    public static IEndpointRouteBuilder MapEvaluationReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/evaluations/{id:int}/report", IssueAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Evaluator));

        endpoints.MapGet("/api/evaluations/{id:int}/report/versions", ListVersionsAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        endpoints.MapGet("/api/evaluations/{id:int}/report", GetCurrentAsync)
            .WithTags("Evaluaciones")
            .RequireAuthorization(policy => policy.RequireRole(
                SystemRoles.Evaluator, SystemRoles.Coordinator, SystemRoles.Administrator));

        return endpoints;
    }

    /// <summary>Historial completo del informe, de la versión más antigua a la más reciente.</summary>
    private static async Task<IResult> ListVersionsAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, instance.CaseId, context, cancellationToken)) return Results.Forbid();

        var reports = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .OrderBy(value => value.Version)
            .ToListAsync(cancellationToken);

        return Results.Ok(reports.Select(Describe));
    }

    /// <summary>Versión vigente del informe, que es siempre la última emitida.</summary>
    private static async Task<IResult> GetCurrentAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (!await CanReadAsync(principal, instance.CaseId, context, cancellationToken)) return Results.Forbid();

        var report = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return report is null ? Results.NotFound() : Results.Ok(Describe(report));
    }

    /// <summary>
    /// El coordinador y la administración supervisan todos los expedientes; el técnico solo lee el
    /// informe del expediente que tiene asignado.
    /// </summary>
    private static async Task<bool> CanReadAsync(
        ClaimsPrincipal principal,
        int caseId,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (principal.IsInRole(SystemRoles.Coordinator) || principal.IsInRole(SystemRoles.Administrator))
            return true;

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId))
            return false;

        return await context.CaseAssignments.AsNoTracking()
            .AnyAsync(value => value.CaseId == caseId && value.TechnicianId == actingUserId, cancellationToken);
    }

    private static async Task<IResult> IssueAsync(
        int id,
        IssueReportRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId))
            return Results.Unauthorized();

        var instance = await context.EvaluationInstances.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();

        // El informe describe una evaluación cerrada. Emitirlo con la captura abierta produciría un
        // documento sobre datos que todavía pueden cambiar.
        if (instance.Status != EvaluationInstanceStatuses.Submitted)
            return Results.Conflict(new { message = "La evaluación todavía no ha sido enviada y no admite informe." });

        // El informe lo emite quien tiene la asignación vigente del expediente, no quien inició la
        // evaluación: una reasignación en curso traslada también la responsabilidad del informe.
        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == instance.CaseId && value.IsCurrent, cancellationToken);
        if (assignment is null || assignment.TechnicianId != actingUserId) return Results.Forbid();

        var lastVersion = await context.EvaluationReports.AsNoTracking()
            .Where(value => value.EvaluationInstanceId == id)
            .MaxAsync(value => (int?)value.Version, cancellationToken) ?? 0;

        var report = new EvaluationReport
        {
            EvaluationInstanceId = id,
            Version = lastVersion + 1,
            ExecutiveSummary = request.ExecutiveSummary.Trim(),
            Findings = request.Findings.Trim(),
            Recommendations = request.Recommendations.Trim(),
            CreatedBy = actingUserId
        };
        context.EvaluationReports.Add(report);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/evaluations/{id}/report/{report.Version}", Describe(report));
    }

    private static EvaluationReportResponse Describe(EvaluationReport report) => new(
        report.Id,
        report.EvaluationInstanceId,
        report.Version,
        report.Status,
        report.ExecutiveSummary,
        report.Findings,
        report.Recommendations,
        report.CreatedAt,
        report.CreatedBy);

    private sealed record IssueReportRequest(string ExecutiveSummary, string Findings, string Recommendations);

    private sealed record EvaluationReportResponse(
        int Id,
        int EvaluationInstanceId,
        int Version,
        string Status,
        string ExecutiveSummary,
        string Findings,
        string Recommendations,
        DateTimeOffset CreatedAt,
        Guid CreatedBy);
}
