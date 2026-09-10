using EBR.Domain.RiskCatalogs;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.Infrastructure.Risk;

/// <summary>
/// Carga de forma idempotente las escalas versionadas, los factores del establecimiento,
/// las bandas de frecuencia y los peligros por subcategoría. Las filas incompletas de la
/// matriz de alimentos se devuelven rechazadas y nunca se promueven.
/// </summary>
public sealed class RiskCatalogSeeder(EbrDbContext context)
{
    public const string ProductScaleCode = "PRODUCT";
    public const string FrequencyScaleCode = "FREQUENCY";

    /// <summary>
    /// Código del factor del establecimiento que representa el cumplimiento con las BPM: es el punto
    /// por el que el resultado de una evaluación entra en el cálculo de riesgo de la empresa.
    /// </summary>
    public const string BpmFactorCode = "BPM";
    private const int CatalogVersion = 1;

    private static readonly (string Code, string Name, decimal Score, int Rank)[] ProductLevels =
    [
        ("LOW", "Riesgo bajo", 2m, 1),
        ("MEDIUM", "Riesgo medio", 4m, 2),
        ("HIGH", "Riesgo alto", 8m, 3)
    ];

    private static readonly (string Code, string Name, decimal Score, int Rank)[] FrequencyLevels =
    [
        ("LOW", "Riesgo bajo", 1m, 1),
        ("MEDIUM", "Riesgo medio", 2m, 2),
        ("HIGH", "Riesgo alto", 3m, 3)
    ];

    private static readonly (string Code, string Name, decimal Weight, string[] Options)[] Factors =
    [
        ("VOLUMEN", "Volumen de producción", 0.16m,
        [
            "Grande (>2 000 000 por mes)",
            "Mediano (800 000-2 000 000 por mes)",
            "Pequeño (200 000-799 000 por mes)",
            "Micro (<200 000 por mes)"
        ]),
        ("HACCP", "Implementación sistema HACCP", 0.09m,
        [
            "No tiene implementado el sistema HACCP",
            "Tiene implementado el HACCP en el 25% de las líneas de producción",
            "Tiene implementado el HACCP en el 75% de las líneas de producción",
            "Tiene implementado el HACCP en todas las líneas de producción"
        ]),
        (BpmFactorCode, "Cumplimiento con las BPM", 0.56m,
        [
            "≤ 81%",
            "82% - 89%",
            "90% - 95%",
            ">95%"
        ]),
        ("INABIE", "Proveedor INABIE", 0.05m,
        [
            "Los productos que elaboran los sirven a nivel nacional",
            "Los productos que elaboran los sirven a nivel regional",
            "Los productos que elaboran los sirven a nivel local",
            "No es suplidor del INABIE"
        ]),
        ("RECHAZOS", "Rechazos de registros sanitarios por incumplimientos microbiológicos", 0.06m,
        [
            "Tiene más de 2 rechazos en los últimos 5 años",
            "Tiene 2 rechazos en los últimos 5 años",
            "Tiene 1 rechazo en los últimos 5 años",
            "No tiene ningún rechazo en los últimos 5 años"
        ]),
        ("MUESTREO", "Planes de muestreo microbiológico y análisis de laboratorio", 0.08m,
        [
            "No cuenta con plan de muestreo microbiológico",
            "Tiene plan de muestreo microbiológico solo para las materias primas",
            "Tiene un plan de muestreo microbiológico solo para las áreas de proceso y productos terminados",
            "Tiene un plan de muestreo microbiológico para las materias primas, las áreas de proceso y productos terminados"
        ])
    ];

    private static readonly decimal[] OptionScores = [3m, 2.33m, 1.67m, 1m];

    private static readonly (string LevelCode, string LevelName, decimal Minimum, bool MinimumIncluded, decimal Maximum, int Months)[] Bands =
    [
        ("LOW", "Riesgo bajo", 1m, true, 3.6m, 12),
        ("MEDIUM", "Riesgo medio", 3.6m, false, 6.3m, 6),
        ("HIGH", "Riesgo alto", 6.3m, false, 24m, 3)
    ];

    public async Task<RiskCatalogSeedResult> SeedAsync(string foodMatrixSource, CancellationToken cancellationToken)
    {
        var import = FoodRiskMatrixImporter.Import(foodMatrixSource);
        var productScale = await EnsureScaleAsync(ProductScaleCode, "Riesgo del alimento", ProductLevels, cancellationToken);
        var frequencyScale = await EnsureScaleAsync(FrequencyScaleCode, "Nivel descriptivo de frecuencia", FrequencyLevels, cancellationToken);
        var ruleVersion = await EnsureRuleVersionAsync(productScale, frequencyScale, cancellationToken);
        await EnsureFactorsAsync(ruleVersion, cancellationToken);
        await EnsureBandsAsync(ruleVersion, frequencyScale, cancellationToken);
        var hazards = await EnsureHazardsAsync(import, productScale, cancellationToken);
        if (!ruleVersion.IsPublished)
        {
            ruleVersion.IsPublished = true;
            await context.SaveChangesAsync(cancellationToken);
        }

        return new RiskCatalogSeedResult(ruleVersion.Id, import.Accepted.Count, hazards, import.Rejected);
    }

    private async Task<RiskScale> EnsureScaleAsync(
        string code,
        string name,
        (string Code, string Name, decimal Score, int Rank)[] levels,
        CancellationToken cancellationToken)
    {
        var scale = await context.RiskScales
            .SingleOrDefaultAsync(item => item.Code == code && item.Version == CatalogVersion, cancellationToken);
        if (scale is null)
        {
            scale = new RiskScale
            {
                Code = code,
                Name = name,
                Version = CatalogVersion,
                EffectiveFrom = DateTimeOffset.UtcNow
            };
            context.RiskScales.Add(scale);
            await context.SaveChangesAsync(cancellationToken);
        }

        var existing = await context.RiskScaleLevels.Where(level => level.ScaleId == scale.Id).ToListAsync(cancellationToken);
        foreach (var level in levels.Where(level => existing.TrueForAll(item => item.Code != level.Code)))
        {
            context.RiskScaleLevels.Add(new RiskScaleLevel
            {
                ScaleId = scale.Id,
                Code = level.Code,
                Name = level.Name,
                Score = level.Score,
                Rank = level.Rank
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return scale;
    }

    private async Task<RiskRuleVersion> EnsureRuleVersionAsync(
        RiskScale productScale,
        RiskScale frequencyScale,
        CancellationToken cancellationToken)
    {
        var ruleVersion = await context.RiskRuleVersions
            .SingleOrDefaultAsync(item => item.Version == CatalogVersion, cancellationToken);
        if (ruleVersion is not null) return ruleVersion;
        ruleVersion = new RiskRuleVersion
        {
            Version = CatalogVersion,
            EffectiveFrom = DateTimeOffset.UtcNow,
            ProductScaleId = productScale.Id,
            FrequencyScaleId = frequencyScale.Id,
            IsPublished = false
        };
        context.RiskRuleVersions.Add(ruleVersion);
        await context.SaveChangesAsync(cancellationToken);
        return ruleVersion;
    }

    private async Task EnsureFactorsAsync(RiskRuleVersion ruleVersion, CancellationToken cancellationToken)
    {
        var existing = await context.StructuralRiskFactors
            .Where(factor => factor.RuleVersionId == ruleVersion.Id)
            .Select(factor => factor.Code)
            .ToListAsync(cancellationToken);
        var order = 0;
        foreach (var factor in Factors)
        {
            order++;
            if (existing.Contains(factor.Code)) continue;
            var entity = new StructuralRiskFactor
            {
                RuleVersionId = ruleVersion.Id,
                Code = factor.Code,
                Name = factor.Name,
                Weight = factor.Weight,
                Order = order
            };
            for (var index = 0; index < factor.Options.Length; index++)
            {
                entity.Options.Add(new StructuralRiskOption
                {
                    Description = factor.Options[index],
                    Score = OptionScores[index],
                    Order = index + 1
                });
            }

            context.StructuralRiskFactors.Add(entity);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBandsAsync(
        RiskRuleVersion ruleVersion,
        RiskScale frequencyScale,
        CancellationToken cancellationToken)
    {
        if (await context.InspectionFrequencyMatrices.AnyAsync(item => item.RuleVersionId == ruleVersion.Id, cancellationToken))
            return;
        var levels = await context.RiskScaleLevels
            .Where(level => level.ScaleId == frequencyScale.Id)
            .ToListAsync(cancellationToken);
        foreach (var band in Bands)
        {
            var scaleLevel = levels.Single(level => level.Code == band.LevelCode);
            var riskLevel = await context.RiskLevels
                .SingleOrDefaultAsync(level => level.Points == scaleLevel.Rank, cancellationToken);
            if (riskLevel is null)
            {
                riskLevel = new RiskLevel { Name = band.LevelName, Points = scaleLevel.Rank };
                context.RiskLevels.Add(riskLevel);
                await context.SaveChangesAsync(cancellationToken);
            }

            context.InspectionFrequencyMatrices.Add(new InspectionFrequencyMatrix
            {
                RuleVersionId = ruleVersion.Id,
                ScaleLevelId = scaleLevel.Id,
                RiskMin = band.Minimum,
                MinimumIncluded = band.MinimumIncluded,
                RiskMax = band.Maximum,
                RiskLevelId = riskLevel.Id,
                FrequencyMonths = band.Months
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> EnsureHazardsAsync(
        FoodRiskMatrixImportResult import,
        RiskScale productScale,
        CancellationToken cancellationToken)
    {
        var levels = await context.RiskScaleLevels
            .Where(level => level.ScaleId == productScale.Id)
            .ToDictionaryAsync(level => level.Score, cancellationToken);
        var categories = await context.FoodCategories.ToDictionaryAsync(item => item.Name, cancellationToken);
        var subcategories = await context.FoodSubcategories.ToListAsync(cancellationToken);
        var hazards = await context.FoodSubcategoryHazards.ToListAsync(cancellationToken);
        var written = 0;
        foreach (var row in import.Accepted)
        {
            if (!categories.TryGetValue(row.Category, out var category))
            {
                category = new FoodCategory { Name = row.Category };
                context.FoodCategories.Add(category);
                await context.SaveChangesAsync(cancellationToken);
                categories[row.Category] = category;
            }

            var subcategory = subcategories.Find(item => item.CategoryId == category.Id && item.Name == row.Subcategory);
            if (subcategory is null)
            {
                subcategory = new FoodSubcategory { CategoryId = category.Id, Name = row.Subcategory };
                context.FoodSubcategories.Add(subcategory);
                await context.SaveChangesAsync(cancellationToken);
                subcategories.Add(subcategory);
            }

            written += AddHazard(hazards, subcategory.Id, "MICROBIOLOGICAL", levels[row.MicrobiologicalScore].Id);
            if (row.ChemicalScore is { } chemical)
                written += AddHazard(hazards, subcategory.Id, "CHEMICAL", levels[chemical].Id);
        }

        await context.SaveChangesAsync(cancellationToken);
        return written;
    }

    private int AddHazard(List<FoodSubcategoryHazard> hazards, int subcategoryId, string hazardType, int levelId)
    {
        if (hazards.Exists(item => item.SubcategoryId == subcategoryId && item.HazardType == hazardType)) return 0;
        var hazard = new FoodSubcategoryHazard
        {
            SubcategoryId = subcategoryId,
            HazardType = hazardType,
            LevelId = levelId
        };
        hazards.Add(hazard);
        context.FoodSubcategoryHazards.Add(hazard);
        return 1;
    }
}

public sealed record RiskCatalogSeedResult(
    int RuleVersionId,
    int AcceptedRows,
    int Hazards,
    IReadOnlyList<FoodRiskMatrixRejection> Rejected);
