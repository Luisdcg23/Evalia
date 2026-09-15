using System.Security.Claims;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

/// <summary>Consultas operativas filtradas por la identidad autenticada.</summary>
public static class OperationalReadEndpoints
{
    public static IEndpointRouteBuilder MapOperationalReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me/cases", MyCasesAsync).RequireAuthorization();
        endpoints.MapGet("/api/me/schedule", MyScheduleAsync)
            .RequireAuthorization(p => p.RequireRole(SystemRoles.Evaluator));
        endpoints.MapGet("/api/technicians", TechniciansAsync)
            .RequireAuthorization(p => p.RequireRole(SystemRoles.Coordinator, SystemRoles.Administrator));
        endpoints.MapGet("/api/cases/history/search", HistoryAsync)
            .RequireAuthorization(p => p.RequireRole(SystemRoles.Coordinator, SystemRoles.Administrator));
        endpoints.MapGet("/api/dashboard", DashboardAsync).RequireAuthorization();
        endpoints.MapGet("/api/notifications", NotificationsAsync).RequireAuthorization();
        endpoints.MapPatch("/api/notifications/{id:int}/read", MarkReadAsync).RequireAuthorization();
        return endpoints;
    }

    private static bool TryUserId(ClaimsPrincipal principal, out Guid id) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out id);

    private static async Task<IResult> MyCasesAsync(
        ClaimsPrincipal principal, EbrDbContext db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var query = db.InspectionCases.AsNoTracking();

        if (principal.IsInRole(SystemRoles.Evaluator))
        {
            query = from item in query
                    join assignment in db.CaseAssignments.AsNoTracking().Where(x => x.IsCurrent)
                        on item.Id equals assignment.CaseId
                    where assignment.TechnicianId == userId
                    select item;
        }
        else if (principal.IsInRole(SystemRoles.CompanyAdministrator) || principal.IsInRole(SystemRoles.DelegateUser))
        {
            query = from item in query
                    join link in db.CompanyUsers.AsNoTracking() on item.CompanyId equals link.CompanyId
                    where link.UserId == userId
                    select item;
        }
        else if (!principal.IsInRole(SystemRoles.Administrator) && !principal.IsInRole(SystemRoles.Coordinator))
        {
            return Results.Forbid();
        }

        var rows = await (from item in query
                          join company in db.Companies.AsNoTracking() on item.CompanyId equals company.Id
                          join instance0 in db.EvaluationInstances.AsNoTracking() on item.Id equals instance0.CaseId into instances
                          from instance in instances.DefaultIfEmpty()
                          join result0 in db.EvaluationResults.AsNoTracking() on instance.Id equals result0.EvaluationInstanceId into results
                          from result in results.DefaultIfEmpty()
                          orderby item.CreatedAt descending
                          select new
                          {
                              item.Id, item.CompanyId, CompanyName = company.TradeName,
                              item.SourceType, item.Priority, item.Status, item.CreatedAt,
                              EvaluationInstanceId = instance == null ? (int?)null : instance.Id,
                              BpmPercentage = result == null ? (decimal?)null : result.BpmPercentage,
                              Classification = result == null ? null : result.Classification,
                              FrequencyMonths = result == null ? (int?)null : result.FrequencyMonths
                          }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> MyScheduleAsync(
        ClaimsPrincipal principal, DateTimeOffset? from, DateTimeOffset? to,
        EbrDbContext db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var start = from ?? DateTimeOffset.UtcNow.AddDays(-1);
        var end = to ?? start.AddDays(31);
        if (end <= start || end - start > TimeSpan.FromDays(366))
            return Results.BadRequest(new { message = "El rango de agenda es inválido." });

        var rows = await (from schedule in db.CaseSchedules.AsNoTracking()
                          join item in db.InspectionCases.AsNoTracking() on schedule.CaseId equals item.Id
                          join company in db.Companies.AsNoTracking() on item.CompanyId equals company.Id
                          where schedule.TechnicianId == userId && schedule.IsCurrent &&
                                schedule.ScheduledFor >= start && schedule.ScheduledFor < end
                          orderby schedule.ScheduledFor
                          select new { CaseId = item.Id, item.CompanyId, CompanyName = company.TradeName,
                              CompanyAddress = company.Address, schedule.ScheduledFor, item.Status,
                              schedule.Priority, schedule.Observations }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> TechniciansAsync(
        UserManager<ApplicationUser> users, EbrDbContext db, CancellationToken ct)
    {
        var technicians = await users.GetUsersInRoleAsync(SystemRoles.Evaluator);
        var approved = technicians.Where(x => x.ApprovalStatus == UserApprovalStatus.Approved).ToList();
        var ids = approved.Select(x => x.Id).ToArray();
        var loads = await db.CaseAssignments.AsNoTracking()
            .Where(x => x.IsCurrent && ids.Contains(x.TechnicianId))
            .GroupBy(x => x.TechnicianId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return Results.Ok(approved.OrderBy(x => x.FullName).Select(x => new
        {
            x.Id, x.FullName, x.Email, ActiveCaseCount = loads.GetValueOrDefault(x.Id)
        }));
    }

    private static async Task<IResult> HistoryAsync(
        int? companyId, Guid? technicianId, string? status, string? sourceType,
        DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize,
        EbrDbContext db, CancellationToken ct)
    {
        var requestedPage = Math.Max(page ?? 1, 1);
        var requestedPageSize = Math.Clamp(pageSize ?? 50, 1, 200);
        var normalizedStatus = status?.Trim().ToUpperInvariant();
        var normalizedSourceType = sourceType?.Trim().ToUpperInvariant();
        var query = from history in db.CaseStateHistories.AsNoTracking()
                    join item in db.InspectionCases.AsNoTracking() on history.CaseId equals item.Id
                    select new { History = history, Case = item };
        if (companyId.HasValue) query = query.Where(x => x.Case.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(normalizedStatus)) query = query.Where(x => x.History.NewStatus == normalizedStatus);
        if (!string.IsNullOrWhiteSpace(normalizedSourceType)) query = query.Where(x => x.Case.SourceType == normalizedSourceType);
        if (from.HasValue) query = query.Where(x => x.History.ChangedAt >= from);
        if (to.HasValue) query = query.Where(x => x.History.ChangedAt < to);
        if (technicianId.HasValue)
            query = query.Where(x => db.CaseAssignments.Any(a => a.CaseId == x.Case.Id && a.TechnicianId == technicianId));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.History.ChangedAt)
            .Skip((requestedPage - 1) * requestedPageSize).Take(requestedPageSize)
            .Select(x => new { x.History.Id, x.History.CaseId, x.Case.CompanyId, x.Case.SourceType,
                x.History.PreviousStatus, x.History.NewStatus, x.History.Reason,
                x.History.ChangedAt, x.History.ChangedBy }).ToListAsync(ct);

        // RF-20: cada expediente del resultado se acompaña de su calificación (Evaluacion_Resultado,
        // si existe) y del informe oficial emitido (Evaluacion_Informe_Oficial, si existe). Es todo
        // lectura sobre tablas ya inmutables; no se recalcula nada.
        var caseIds = rows.Select(x => x.CaseId).Distinct().ToArray();
        var instances = await db.EvaluationInstances.AsNoTracking()
            .Where(x => caseIds.Contains(x.CaseId))
            .Select(x => new { x.Id, x.CaseId })
            .ToListAsync(ct);
        var instanceByCase = instances.ToDictionary(x => x.CaseId, x => x.Id);
        var instanceIds = instances.Select(x => x.Id).ToArray();

        var results = await (from result in db.EvaluationResults.AsNoTracking()
                             where instanceIds.Contains(result.EvaluationInstanceId)
                             join calculation in db.RiskCalculations.AsNoTracking()
                                 on result.RiskCalculationId equals calculation.Id
                             join level in db.RiskLevels.AsNoTracking()
                                 on calculation.RiskLevelId equals level.Id
                             select new
                             {
                                 result.EvaluationInstanceId,
                                 result.BpmPercentage,
                                 result.QualificationCode,
                                 result.Classification,
                                 result.BpmRiskScore,
                                 result.FrequencyMonths,
                                 RiskLevel = level.Name
                             }).ToListAsync(ct);
        var resultByInstance = results.ToDictionary(x => x.EvaluationInstanceId);

        var officialReports = await (from report in db.EvaluationReports.AsNoTracking()
                                     where instanceIds.Contains(report.EvaluationInstanceId)
                                     join official in db.EvaluationOfficialReports.AsNoTracking()
                                         on report.Id equals official.ReportId
                                     select new
                                     {
                                         report.EvaluationInstanceId,
                                         official.GeneratedAt,
                                         official.Sha256,
                                         official.FileName,
                                         official.SizeBytes
                                     }).ToListAsync(ct);
        var officialByInstance = officialReports
            .GroupBy(x => x.EvaluationInstanceId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.GeneratedAt).First());

        var items = rows.Select(x =>
        {
            instanceByCase.TryGetValue(x.CaseId, out var instanceId);
            var hasInstance = instanceByCase.ContainsKey(x.CaseId);
            var evaluation = hasInstance && resultByInstance.TryGetValue(instanceId, out var result)
                ? new
                {
                    result.BpmPercentage,
                    result.QualificationCode,
                    result.Classification,
                    result.BpmRiskScore,
                    result.RiskLevel,
                    result.FrequencyMonths
                }
                : null;
            var officialReport = hasInstance && officialByInstance.TryGetValue(instanceId, out var official)
                ? new { official.GeneratedAt, official.Sha256, official.FileName, official.SizeBytes }
                : null;
            return new
            {
                x.Id, x.CaseId, x.CompanyId, x.SourceType,
                x.PreviousStatus, x.NewStatus, x.Reason, x.ChangedAt, x.ChangedBy,
                HasOfficialReport = officialReport is not null,
                OfficialReport = officialReport,
                Evaluation = evaluation
            };
        }).ToList();
        return Results.Ok(new { page = requestedPage, pageSize = requestedPageSize, total, items });
    }

    private static async Task<IResult> DashboardAsync(
        ClaimsPrincipal principal, EbrDbContext db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        IQueryable<InspectionCase> cases = db.InspectionCases.AsNoTracking();
        if (principal.IsInRole(SystemRoles.Evaluator))
            cases = from item in cases join a in db.CaseAssignments.AsNoTracking().Where(x => x.IsCurrent && x.TechnicianId == userId)
                    on item.Id equals a.CaseId select item;
        else if (principal.IsInRole(SystemRoles.CompanyAdministrator) || principal.IsInRole(SystemRoles.DelegateUser))
            cases = from item in cases join link in db.CompanyUsers.AsNoTracking().Where(x => x.UserId == userId)
                    on item.CompanyId equals link.CompanyId select item;
        var byStatus = await cases.GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        return Results.Ok(new
        {
            TotalCases = await cases.CountAsync(ct),
            ByStatus = byStatus,
            UnreadNotifications = await db.Notifications.CountAsync(x => x.RecipientId == userId && x.ReadAt == null, ct)
        });
    }

    private static async Task<IResult> NotificationsAsync(
        ClaimsPrincipal principal, bool? unreadOnly, int? take, EbrDbContext db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var requestedTake = Math.Clamp(take ?? 50, 1, 200);
        var query = db.Notifications.AsNoTracking().Where(x => x.RecipientId == userId);
        if (unreadOnly == true) query = query.Where(x => x.ReadAt == null);
        return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).Take(requestedTake).ToListAsync(ct));
    }

    private static async Task<IResult> MarkReadAsync(
        int id, ClaimsPrincipal principal, EbrDbContext db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var item = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.RecipientId == userId, ct);
        if (item is null) return Results.NotFound();
        item.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }
}
