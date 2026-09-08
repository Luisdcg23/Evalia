using EBR.Domain.Identity;
using EBR.Domain.RiskCatalogs;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static class RiskCatalogEndpoints
{
    public static IEndpointRouteBuilder MapRiskCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/catalogs").WithTags("Catálogos de riesgo").RequireAuthorization();
        group.MapGet("/risk-levels", ListRiskLevelsAsync);
        group.MapPost("/risk-levels", CreateRiskLevelAsync).RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/food-categories", ListFoodCategoriesAsync);
        group.MapPost("/food-categories", CreateFoodCategoryAsync).RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/structural-factors", ListFactorsAsync);
        group.MapPost("/structural-factors", CreateFactorAsync).RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        group.MapGet("/frequency-matrix", ListFrequencyMatrixAsync);
        group.MapPost("/frequency-matrix", CreateFrequencyMatrixAsync).RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        endpoints.MapGet("/api/subcategories", ListSubcategoriesAsync).RequireAuthorization();
        endpoints.MapPost("/api/subcategories", CreateSubcategoryAsync).RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));
        return endpoints;
    }

    private static async Task<IResult> ListRiskLevelsAsync(EbrDbContext context, CancellationToken cancellationToken)
    {
        var levels = await context.RiskLevels.AsNoTracking().OrderBy(level => level.Points).ToListAsync(cancellationToken);
        return Results.Ok(levels);
    }

    private static async Task<IResult> CreateRiskLevelAsync(
        CreateRiskLevelRequest request,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Points <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["riskLevel"] = ["Nombre y puntos positivos son obligatorios."]
            });
        }

        if (await context.RiskLevels.AnyAsync(level => level.Name == request.Name.Trim() || level.Points == request.Points, cancellationToken))
            return Results.Conflict();
        var level = new RiskLevel { Name = request.Name.Trim(), Points = request.Points };
        context.RiskLevels.Add(level);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/catalogs/risk-levels/{level.Id}", level);
    }

    private sealed record CreateRiskLevelRequest(string Name, int Points);

    private static async Task<IResult> ListFoodCategoriesAsync(EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.FoodCategories.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateFoodCategoryAsync(
        CreateNameRequest request,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest();
        if (await context.FoodCategories.AnyAsync(item => item.Name == name, cancellationToken)) return Results.Conflict();
        var category = new FoodCategory { Name = name };
        context.FoodCategories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/catalogs/food-categories/{category.Id}", category);
    }

    private static async Task<IResult> ListSubcategoriesAsync(EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.FoodSubcategories.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateSubcategoryAsync(
        CreateSubcategoryRequest request,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var categoryExists = await context.FoodCategories.AnyAsync(item => item.Id == request.CategoryId, cancellationToken);
        var riskLevels = await context.RiskLevels.Where(level =>
                level.Id == request.MicrobiologicalRiskLevelId || level.Id == request.ChemicalRiskLevelId)
            .ToListAsync(cancellationToken);
        if (!categoryExists || riskLevels.Select(level => level.Id).Distinct().Count() !=
            new[] { request.MicrobiologicalRiskLevelId, request.ChemicalRiskLevelId }.Distinct().Count())
            return Results.BadRequest();
        var microbiological = riskLevels.Single(level => level.Id == request.MicrobiologicalRiskLevelId);
        var chemical = riskLevels.Single(level => level.Id == request.ChemicalRiskLevelId);
        var total = microbiological.Points >= chemical.Points ? microbiological : chemical;
        var subcategory = new FoodSubcategory
        {
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            MicrobiologicalRiskLevelId = microbiological.Id,
            MicrobiologicalScore = microbiological.Points,
            ChemicalRiskLevelId = chemical.Id,
            ChemicalScore = chemical.Points,
            TotalRiskLevelId = total.Id,
            TotalScore = total.Points
        };
        context.FoodSubcategories.Add(subcategory);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/subcategories/{subcategory.Id}", subcategory);
    }

    private static async Task<IResult> ListFactorsAsync(EbrDbContext context, CancellationToken cancellationToken)
    {
        var factors = await context.StructuralRiskFactors.AsNoTracking().Include(item => item.Options)
            .OrderBy(item => item.Order).ToListAsync(cancellationToken);
        return Results.Ok(factors);
    }

    private static async Task<IResult> CreateFactorAsync(
        CreateFactorRequest request,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) ||
            request.Weight <= 0 || request.Options.Count == 0) return Results.BadRequest();
        var factor = new StructuralRiskFactor
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Weight = request.Weight,
            Order = request.Order
        };
        foreach (var option in request.Options.OrderBy(item => item.Order))
        {
            factor.Options.Add(new StructuralRiskOption
            {
                Description = option.Description.Trim(),
                Score = option.Score,
                Order = option.Order
            });
        }
        context.StructuralRiskFactors.Add(factor);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/catalogs/structural-factors/{factor.Id}", factor);
    }

    private static async Task<IResult> ListFrequencyMatrixAsync(EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.InspectionFrequencyMatrices.AsNoTracking().OrderBy(item => item.RiskMin).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateFrequencyMatrixAsync(
        CreateFrequencyMatrixRequest request,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (request.RiskMax <= request.RiskMin || request.FrequencyMonths <= 0 ||
            !await context.RiskLevels.AnyAsync(level => level.Id == request.RiskLevelId, cancellationToken))
            return Results.BadRequest();
        var matrix = new InspectionFrequencyMatrix
        {
            RiskMin = request.RiskMin,
            MinimumIncluded = request.MinimumIncluded,
            RiskMax = request.RiskMax,
            RiskLevelId = request.RiskLevelId,
            FrequencyMonths = request.FrequencyMonths
        };
        context.InspectionFrequencyMatrices.Add(matrix);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/catalogs/frequency-matrix/{matrix.Id}", matrix);
    }

    private sealed record CreateNameRequest(string Name);
    private sealed record CreateSubcategoryRequest(
        int CategoryId,
        string Name,
        int MicrobiologicalRiskLevelId,
        int ChemicalRiskLevelId);
    private sealed record CreateFactorOptionRequest(string Description, decimal Score, int Order);
    private sealed record CreateFactorRequest(
        string Code,
        string Name,
        decimal Weight,
        int Order,
        IReadOnlyList<CreateFactorOptionRequest> Options);
    private sealed record CreateFrequencyMatrixRequest(
        decimal RiskMin,
        bool MinimumIncluded,
        decimal RiskMax,
        int RiskLevelId,
        int FrequencyMonths);
}
