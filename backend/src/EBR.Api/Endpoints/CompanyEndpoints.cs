using System.Security.Claims;
using EBR.Domain.Companies;
using EBR.Domain.Identity;
using EBR.Domain.RiskCatalogs;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/companies").WithTags("Empresas").RequireAuthorization();
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapDelete("/{id:int}", DeleteAsync).RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/{id:int}/representatives", ListRepresentativesAsync);
        group.MapPost("/{id:int}/representatives", CreateRepresentativeAsync).RequireAuthorization(policy =>
            policy.RequireRole(SystemRoles.Administrator, SystemRoles.CompanyAdministrator));
        group.MapPost("/{id:int}/subcategories", SetSubcategoriesAsync).RequireAuthorization(policy =>
            policy.RequireRole(SystemRoles.Administrator, SystemRoles.CompanyAdministrator));
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var query = context.Companies.AsNoTracking().Where(company => company.IsActive);
        if (principal.IsInRole(SystemRoles.CompanyAdministrator) || principal.IsInRole(SystemRoles.DelegateUser))
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Results.Unauthorized();
            }

            query = query.Where(company => context.CompanyUsers.Any(
                link => link.CompanyId == company.Id && link.UserId == userId));
        }

        var companies = await query.OrderBy(company => company.LegalName).ToListAsync(cancellationToken);
        return Results.Ok(companies.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var companyRole = principal.IsInRole(SystemRoles.CompanyAdministrator) || principal.IsInRole(SystemRoles.DelegateUser);
        var allowed = !companyRole;
        if (companyRole && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            allowed = await context.CompanyUsers.AnyAsync(
                link => link.CompanyId == id && link.UserId == userId,
                cancellationToken);
        }

        if (!allowed)
        {
            return Results.NotFound();
        }

        var company = await context.Companies.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == id && item.IsActive,
            cancellationToken);
        return company is null ? Results.NotFound() : Results.Ok(ToResponse(company));
    }

    private static async Task<IResult> CreateAsync(
        CreateCompanyRequest request,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var rnc = request.Rnc.Trim();
        if (string.IsNullOrWhiteSpace(request.LegalName) || string.IsNullOrWhiteSpace(rnc) ||
            string.IsNullOrWhiteSpace(request.TradeName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["company"] = ["Razón social, RNC y nombre comercial son obligatorios."]
            });
        }

        if (await context.Companies.AnyAsync(company => company.Rnc == rnc, cancellationToken))
        {
            return Results.Conflict(new { message = "Ya existe una empresa con ese RNC." });
        }

        var company = new Company
        {
            LegalName = request.LegalName.Trim(),
            Rnc = rnc,
            TradeName = request.TradeName.Trim()
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/companies/{company.Id}", ToResponse(company));
    }

    private static object ToResponse(Company company) => new
    {
        company.Id,
        company.LegalName,
        company.Rnc,
        company.TradeName,
        company.IsActive
    };

    private static async Task<IResult> ListRepresentativesAsync(
        int id,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(id, principal, context, cancellationToken)) return Results.NotFound();
        var representatives = await context.CompanyRepresentatives.AsNoTracking()
            .Where(item => item.CompanyId == id && item.IsActive)
            .OrderBy(item => item.FullName)
            .ToListAsync(cancellationToken);
        return Results.Ok(representatives);
    }

    private static async Task<IResult> CreateRepresentativeAsync(
        int id,
        CreateRepresentativeRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(id, principal, context, cancellationToken)) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.DocumentNumber) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["representative"] = ["Todos los datos del representante son obligatorios."]
            });
        }

        var representative = new CompanyRepresentative
        {
            CompanyId = id,
            FullName = request.FullName.Trim(),
            DocumentNumber = request.DocumentNumber.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.PhoneNumber.Trim()
        };
        context.CompanyRepresentatives.Add(representative);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/companies/{id}/representatives/{representative.Id}", representative);
    }

    private static async Task<IResult> DeleteAsync(int id, EbrDbContext context, CancellationToken cancellationToken)
    {
        var company = await context.Companies.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (company is null || !company.IsActive) return Results.NotFound();
        var hasRelatedData = await context.CompanyUsers.AnyAsync(link => link.CompanyId == id, cancellationToken) ||
            await context.CompanyRepresentatives.AnyAsync(item => item.CompanyId == id, cancellationToken);
        if (hasRelatedData) return Results.Conflict(new { message = "La empresa tiene datos relacionados y no puede eliminarse." });
        company.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<bool> CanAccessCompanyAsync(
        int companyId,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (principal.IsInRole(SystemRoles.Administrator))
        {
            return await context.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);
        }

        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            await context.CompanyUsers.AnyAsync(link => link.CompanyId == companyId && link.UserId == userId, cancellationToken);
    }

    private static async Task<IResult> SetSubcategoriesAsync(
        int id,
        SetSubcategoriesRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(id, principal, context, cancellationToken)) return Results.NotFound();
        var requestedIds = request.SubcategoryIds.Distinct().ToArray();
        var existingCount = await context.FoodSubcategories.CountAsync(
            item => requestedIds.Contains(item.Id), cancellationToken);
        if (existingCount != requestedIds.Length) return Results.BadRequest();
        var current = await context.CompanyFoodSubcategories.Where(item => item.CompanyId == id).ToListAsync(cancellationToken);
        context.CompanyFoodSubcategories.RemoveRange(current);
        context.CompanyFoodSubcategories.AddRange(requestedIds.Select(subcategoryId => new CompanyFoodSubcategory
        {
            CompanyId = id,
            SubcategoryId = subcategoryId
        }));
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private sealed record CreateCompanyRequest(string LegalName, string Rnc, string TradeName);

    private sealed record CreateRepresentativeRequest(string FullName, string DocumentNumber, string Email, string PhoneNumber);
    private sealed record SetSubcategoriesRequest(IReadOnlyList<int> SubcategoryIds);
}
