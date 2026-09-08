using System.Security.Claims;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class CaseEndpoints
{
    public static IEndpointRouteBuilder MapCaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/cases").WithTags("Expedientes")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Coordinator));
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapGet("/{id:int}/history", HistoryAsync);
        group.MapPost("/{id:int}/transition", TransitionAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.InspectionCases.AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(int id, EbrDbContext context, CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> HistoryAsync(int id, EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.CaseStateHistories.AsNoTracking().Where(item => item.CaseId == id)
            .OrderBy(item => item.ChangedAt).ToListAsync(cancellationToken));

    private static async Task<IResult> TransitionAsync(int id, TransitionRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();
        var target = request.NewStatus.Trim().ToUpperInvariant();
        if (!CaseStateMachine.CanTransition(item.Status, target))
            return Results.Conflict(new { message = $"No se permite cambiar de {item.Status} a {target}." });
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
        var previous = item.Status;
        item.Status = target;
        context.CaseStateHistories.Add(new CaseStateHistory
        {
            CaseId = item.Id,
            PreviousStatus = previous,
            NewStatus = target,
            Reason = request.Reason.Trim(),
            ChangedBy = userId
        });
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private sealed record TransitionRequest(string NewStatus, string Reason);
}
