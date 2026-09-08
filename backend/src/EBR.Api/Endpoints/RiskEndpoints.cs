using System.Security.Claims;
using EBR.Application.Risk;
using EBR.Domain.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class RiskEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/risk/calculate", CalculateAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Coordinator));
        endpoints.MapGet("/api/companies/{companyId:int}/risk", HistoryAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> CalculateAsync(
        CalculateRiskRequest request,
        ClaimsPrincipal principal,
        IRiskCalculationService service,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
        try
        {
            var result = await service.CalculateAsync(new RiskCalculationCommand(
                request.CompanyId,
                request.FactorSelections.Select(item => new RiskFactorSelection(item.FactorId, item.OptionId)).ToArray(),
                userId), cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> HistoryAsync(
        int companyId,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var companyRole = principal.IsInRole(SystemRoles.CompanyAdministrator) || principal.IsInRole(SystemRoles.DelegateUser);
        if (companyRole)
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
                !await context.CompanyUsers.AnyAsync(item => item.CompanyId == companyId && item.UserId == userId, cancellationToken))
                return Results.NotFound();
        }
        var history = await context.RiskCalculations.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .Join(context.InspectionFrequencyMatrices, item => item.InspectionFrequencyMatrixId, matrix => matrix.Id, (item, matrix) => new RiskCalculationResult(
                item.Id,
                item.CompanyId,
                item.CalculatedAt,
                item.ProductRisk,
                item.EstablishmentRisk,
                item.TotalRisk,
                item.RiskLevelId,
                matrix.FrequencyMonths,
                item.CalculatedAt.AddMonths(matrix.FrequencyMonths),
                item.FactorDetailsJson))
            .OrderByDescending(item => item.CalculatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(history);
    }

    private sealed record FactorSelectionRequest(int FactorId, int OptionId);
    private sealed record CalculateRiskRequest(int CompanyId, IReadOnlyList<FactorSelectionRequest> FactorSelections);
}
