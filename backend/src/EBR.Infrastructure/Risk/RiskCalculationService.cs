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
        var productScores = await context.CompanyFoodSubcategories
            .Where(link => link.CompanyId == command.CompanyId)
            .Join(context.FoodSubcategories, link => link.SubcategoryId, item => item.Id, (_, item) => new
            {
                item.Id,
                item.Name,
                item.TotalScore
            })
            .ToListAsync(cancellationToken);
        if (productScores.Count == 0) throw new ArgumentException("La empresa no tiene subcategorías configuradas.", nameof(command));

        var factorSnapshots = new List<FactorSnapshot>();
        foreach (var selection in command.FactorSelections)
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
            factorSnapshots.Add(new FactorSnapshot(
                selected.Factor.Id,
                selected.Factor.Code,
                selected.Factor.Name,
                selected.Factor.Weight,
                selected.Option.Id,
                selected.Option.Description,
                selected.Option.Score));
        }
        if (factorSnapshots.Count == 0) throw new ArgumentException("Debe seleccionar al menos un factor.", nameof(command));

        var matrices = await context.InspectionFrequencyMatrices
            .Join(context.RiskLevels, matrix => matrix.RiskLevelId, level => level.Id, (matrix, level) => new
            {
                Matrix = matrix,
                Level = level
            })
            .ToListAsync(cancellationToken);
        var formula = formulaService.Calculate(new RiskFormulaInput(
            productScores.Select(item => item.TotalScore).ToArray(),
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

        var now = DateTimeOffset.UtcNow;
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
            products = productScores,
            factors = factorSnapshots,
            formula = new { formula.ProductRisk, formula.EstablishmentRisk, formula.TotalRisk }
        }, SnapshotJsonOptions);
        var calculation = new RiskCalculation
        {
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

    private sealed record FactorSnapshot(
        int FactorId,
        string Code,
        string Name,
        decimal Weight,
        int OptionId,
        string Description,
        decimal Score);
}
