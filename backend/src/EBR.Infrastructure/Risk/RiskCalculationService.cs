using System.Text.Encodings.Web;
using System.Text.Json;
using EBR.Application.Risk;
using EBR.Domain.RiskCatalogs;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Infrastructure.Risk;

public sealed class RiskCalculationService(EbrDbContext context, IRiskFormulaService formulaService)
    : IRiskCalculationService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<RiskCalculationResult> CalculateAsync(
        RiskCalculationCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var ruleVersion = await context.RiskRuleVersions
            .Where(version => version.IsPublished && version.IsActive && version.EffectiveFrom <= now &&
                (version.EffectiveTo == null || version.EffectiveTo > now))
            .Where(version => command.RuleVersionId == null || version.Id == command.RuleVersionId)
            .OrderByDescending(version => version.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (ruleVersion is null)
            throw new InvalidOperationException("No existe una versión publicada y vigente de reglas de riesgo.");

        var subcategoryIds = await context.CompanyFoodSubcategories
            .Where(link => link.CompanyId == command.CompanyId)
            .Select(link => link.SubcategoryId)
            .ToListAsync(cancellationToken);
        if (subcategoryIds.Count == 0)
            throw new ArgumentException("La empresa no tiene subcategorías configuradas.", nameof(command));

        var products = await HazardProductScoresAsync(subcategoryIds, ruleVersion, cancellationToken);
        if (products.Count == 0)
        {
            throw new ArgumentException(
                "No existen peligros conocidos para las subcategorías de la empresa.", nameof(command));
        }

        var factorSnapshots = await BuildFactorSnapshotsAsync(command, ruleVersion, cancellationToken);
        var matrices = await BandsOf(ruleVersion)
            .Join(context.RiskLevels, matrix => matrix.RiskLevelId, level => level.Id, (matrix, level) => new
            {
                Matrix = matrix,
                Level = level
            })
            .ToListAsync(cancellationToken);
        var formula = formulaService.Calculate(new RiskFormulaInput(
            products.Select(item => item.Score).ToArray(),
            factorSnapshots.Select(item => new RiskFactorInput(item.Code, item.Score, item.Weight)).ToArray(),
            matrices.Select(item => new RiskFrequencyBand(
                item.Matrix.RiskMin,
                item.Matrix.MinimumIncluded,
                item.Matrix.RiskMax,
                item.Level.Name,
                item.Matrix.FrequencyMonths)).ToArray()));
        var matched = matrices.Single(item =>
            (item.Matrix.MinimumIncluded ? formula.TotalRisk >= item.Matrix.RiskMin : formula.TotalRisk > item.Matrix.RiskMin) &&
            formula.TotalRisk <= item.Matrix.RiskMax);

        var selectedFactorIds = factorSnapshots.Select(item => item.FactorId).ToArray();
        var currentValues = await context.CompanyRiskFactorValues.Where(item =>
            item.CompanyId == command.CompanyId && item.IsCurrent && selectedFactorIds.Contains(item.FactorId))
            .ToListAsync(cancellationToken);
        foreach (var current in currentValues) current.IsCurrent = false;
        context.CompanyRiskFactorValues.AddRange(factorSnapshots.Select(item => new CompanyRiskFactorValue
        {
            CompanyId = command.CompanyId,
            FactorId = item.FactorId,
            OptionId = item.OptionId,
            RegisteredAt = now,
            IsCurrent = true,
            RegisteredBy = command.GeneratedBy
        }));

        var snapshot = JsonSerializer.Serialize(new
        {
            ruleVersion = new
                {
                    id = ruleVersion.Id,
                    version = ruleVersion.Version,
                    aggregationMethod = ruleVersion.AggregationMethod,
                    productScaleId = ruleVersion.ProductScaleId,
                    frequencyScaleId = ruleVersion.FrequencyScaleId
                },
            products,
            factors = factorSnapshots,
            formula = new { formula.ProductRisk, formula.EstablishmentRisk, formula.TotalRisk }
        }, SnapshotJsonOptions);
        var calculation = new RiskCalculation
        {
            RuleVersionId = ruleVersion.Id,
            CompanyId = command.CompanyId,
            CalculatedAt = now,
            ProductRisk = formula.ProductRisk,
            EstablishmentRisk = formula.EstablishmentRisk,
            TotalRisk = formula.TotalRisk,
            RiskLevelId = matched.Level.Id,
            InspectionFrequencyMatrixId = matched.Matrix.Id,
            FactorDetailsJson = snapshot,
            GeneratedBy = command.GeneratedBy
        };
        context.RiskCalculations.Add(calculation);
        await context.SaveChangesAsync(cancellationToken);
        return new RiskCalculationResult(
            calculation.Id,
            calculation.CompanyId,
            calculation.CalculatedAt,
            calculation.ProductRisk,
            calculation.EstablishmentRisk,
            calculation.TotalRisk,
            calculation.RiskLevelId,
            matched.Matrix.FrequencyMonths,
            calculation.CalculatedAt.AddMonths(matched.Matrix.FrequencyMonths),
            calculation.FactorDetailsJson);
    }

    private IQueryable<InspectionFrequencyMatrix> BandsOf(RiskRuleVersion ruleVersion)
    {
        var ruleVersionId = ruleVersion.Id;
        return context.InspectionFrequencyMatrices.Where(matrix => matrix.RuleVersionId == ruleVersionId);
    }

    private async Task<List<ProductSnapshot>> HazardProductScoresAsync(
        IReadOnlyList<int> subcategoryIds,
        RiskRuleVersion ruleVersion,
        CancellationToken cancellationToken) =>
        await context.FoodSubcategoryHazards
            .Where(hazard => subcategoryIds.Contains(hazard.SubcategoryId))
            .Join(context.RiskScaleLevels.Where(level => level.ScaleId == ruleVersion.ProductScaleId),
                hazard => hazard.LevelId,
                level => level.Id,
                (hazard, level) => new { hazard, level })
            .Join(context.FoodSubcategories,
                item => item.hazard.SubcategoryId,
                subcategory => subcategory.Id,
                (item, subcategory) => new ProductSnapshot(
                    subcategory.Id,
                    subcategory.Name,
                    item.hazard.HazardType,
                    item.hazard.ScoreOverride ?? item.level.Score))
            .ToListAsync(cancellationToken);

    private async Task<List<FactorSnapshot>> BuildFactorSnapshotsAsync(
        RiskCalculationCommand command,
        RiskRuleVersion ruleVersion,
        CancellationToken cancellationToken)
    {
        var selections = command.FactorSelections;
        if (selections.Count == 0)
            throw new ArgumentException("Debe seleccionar al menos un factor.", nameof(command));
        if (selections.Select(selection => selection.FactorId).Distinct().Count() != selections.Count)
            throw new ArgumentException("No se permiten factores duplicados.", nameof(command));
        {
            var required = await context.StructuralRiskFactors
                .Where(factor => factor.RuleVersionId == ruleVersion.Id && factor.IsActive)
                .Select(factor => factor.Id)
                .ToListAsync(cancellationToken);
            if (!required.OrderBy(id => id).SequenceEqual(selections.Select(item => item.FactorId).OrderBy(id => id)))
            {
                throw new ArgumentException(
                    "La selección debe cubrir exactamente los factores activos de la versión de reglas.", nameof(command));
            }
        }

        var snapshots = new List<FactorSnapshot>();
        foreach (var selection in selections)
        {
            var selected = await context.StructuralRiskOptions
                .Where(option => option.Id == selection.OptionId && option.FactorId == selection.FactorId)
                .Join(context.StructuralRiskFactors, option => option.FactorId, factor => factor.Id, (option, factor) => new
                {
                    Factor = factor,
                    Option = option
                })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException("Una opción no pertenece al factor indicado.", nameof(command));
            snapshots.Add(new FactorSnapshot(
                selected.Factor.Id,
                selected.Factor.Code,
                selected.Factor.Name,
                selected.Factor.Weight,
                selected.Option.Id,
                selected.Option.Description,
                selected.Option.Score));
        }

        return snapshots;
    }

    private sealed record ProductSnapshot(int SubcategoryId, string Name, string HazardType, decimal Score);

    private sealed record FactorSnapshot(
        int FactorId,
        string Code,
        string Name,
        decimal Weight,
        int OptionId,
        string Description,
        decimal Score);
}
