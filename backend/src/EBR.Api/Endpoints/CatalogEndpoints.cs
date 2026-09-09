using System.Globalization;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

/// <summary>
/// Catálogos generales de apoyo, consultados por código, para poblar listas de selección
/// en la interfaz sin exponer cada tabla o constante mediante un endpoint dedicado.
/// </summary>
public static class CatalogEndpoints
{
    private const string RiskLevelsCode = "NIVELES_RIESGO";
    private const string FoodCategoriesCode = "CATEGORIAS_ALIMENTO";
    private const string SystemRolesCode = "ROLES_SISTEMA";
    private const string BpmRequestStatusesCode = "ESTADOS_SOLICITUD";
    private const string CaseStatusesCode = "ESTADOS_CASO";

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/catalogs")
            .WithTags("Catálogos generales")
            .RequireAuthorization()
            .MapGet("/{code}", GetCatalogAsync);
        return endpoints;
    }

    private static async Task<IResult> GetCatalogAsync(
        string code,
        EbrDbContext context,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        IReadOnlyList<CatalogOptionResponse> options = normalizedCode switch
        {
            RiskLevelsCode => (await context.RiskLevels.AsNoTracking()
                    .OrderBy(level => level.Points)
                    .ToListAsync(cancellationToken))
                .Select(level => new CatalogOptionResponse(level.Id.ToString(CultureInfo.InvariantCulture), level.Name))
                .ToList(),
            FoodCategoriesCode => (await context.FoodCategories.AsNoTracking()
                    .OrderBy(category => category.Name)
                    .ToListAsync(cancellationToken))
                .Select(category => new CatalogOptionResponse(category.Id.ToString(CultureInfo.InvariantCulture), category.Name))
                .ToList(),
            SystemRolesCode => await GetSystemRolesAsync(roleManager, cancellationToken),
            BpmRequestStatusesCode => BpmRequestStatuses.All
                .Select(status => new CatalogOptionResponse(status, status))
                .ToList(),
            CaseStatusesCode => CaseStatuses.All
                .Select(status => new CatalogOptionResponse(status, status))
                .ToList(),
            _ => Array.Empty<CatalogOptionResponse>()
        };

        if (options.Count == 0 && !IsKnownCatalog(normalizedCode))
        {
            return Results.NotFound();
        }

        return Results.Ok(options);
    }

    private static bool IsKnownCatalog(string normalizedCode) => normalizedCode is
        RiskLevelsCode or FoodCategoriesCode or SystemRolesCode or BpmRequestStatusesCode or CaseStatusesCode;

    private static async Task<IReadOnlyList<CatalogOptionResponse>> GetSystemRolesAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles.AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);
        return roles.Select(name => new CatalogOptionResponse(name, name)).ToList();
    }

    private sealed record CatalogOptionResponse(string Code, string Name);
}
