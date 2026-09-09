using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.EntityFrameworkCore;

namespace EBR.IntegrationTests;

public sealed class RiskCatalogSeedTests
{
    private const string Source = """
        ===== SHEET: Categorización_de_alimentos (1002x28) =====
        A1=CATEGORIA | B1=SUBCATEGORIA | C1=RIESGO MICROBIOLÓGICO | D1=PUNTAJE | E1=RIESGO QUÍMICO | F1=PUNTAJE | G1=RIESGO TOTAL
        A2=Productos lácteos | B2=Grasa láctea | C2=BAJO | D2=2 | G2=2
        A3=Frutas y Hortalizas | B3=Frutas en conserva | C3=BAJO | D3=2 | E3=MEDIO | F3=4 | G3=3
        A4=Frutas y Hortalizas | B4=Pulpas y preparados de hortalizas
        A5=Carne y productos cárnicos | B5=Crudo intacto | C5=ALTO | D5=8 | E5=ALTO | G5=8
        """;

    [Fact]
    public async Task SeedPublishesTheRuleVersionAndKeepsIncompleteRowsOutOfTheModel()
    {
        await using var context = NewContext();

        var result = await new RiskCatalogSeeder(context).SeedAsync(Source, CancellationToken.None);

        var version = await context.RiskRuleVersions.SingleAsync();
        var factors = await context.StructuralRiskFactors.Where(item => item.RuleVersionId == version.Id).ToListAsync();
        Assert.True(version.IsPublished);
        Assert.Equal("MAX", version.AggregationMethod);
        Assert.Equal(EbrDbContext.PublishedRuleFactorCount, factors.Count);
        Assert.Equal(1m, factors.Sum(factor => factor.Weight));
        Assert.Equal(24, await context.StructuralRiskOptions.CountAsync());
        Assert.Equal([2m, 4m, 8m], await ScoresAsync(context, RiskCatalogSeeder.ProductScaleCode));
        Assert.Equal([1m, 2m, 3m], await ScoresAsync(context, RiskCatalogSeeder.FrequencyScaleCode));
        Assert.Equal([12, 6, 3], await context.InspectionFrequencyMatrices
            .Where(item => item.RuleVersionId == version.Id)
            .OrderBy(item => item.RiskMin)
            .Select(item => item.FrequencyMonths)
            .ToListAsync());

        Assert.Equal(2, result.Rejected.Count);
        Assert.Equal(2, result.AcceptedRows);
        Assert.Equal(2, await context.FoodSubcategories.CountAsync());
        Assert.Equal(3, await context.FoodSubcategoryHazards.CountAsync());
        Assert.DoesNotContain(
            await context.FoodSubcategories.Select(item => item.Name).ToListAsync(),
            name => name.StartsWith("Pulpas", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeedIsIdempotentAndDoesNotDuplicateCatalogs()
    {
        await using var context = NewContext();
        var seeder = new RiskCatalogSeeder(context);

        await seeder.SeedAsync(Source, CancellationToken.None);
        var second = await seeder.SeedAsync(Source, CancellationToken.None);

        Assert.Equal(0, second.Hazards);
        Assert.Single(await context.RiskRuleVersions.ToListAsync());
        Assert.Equal(EbrDbContext.PublishedRuleFactorCount, await context.StructuralRiskFactors.CountAsync());
        Assert.Equal(3, await context.InspectionFrequencyMatrices.CountAsync());
        Assert.Equal(3, await context.FoodSubcategoryHazards.CountAsync());
    }

    private static async Task<List<decimal>> ScoresAsync(EbrDbContext context, string code) =>
        await context.RiskScales.Where(scale => scale.Code == code)
            .Join(context.RiskScaleLevels, scale => scale.Id, level => level.ScaleId, (_, level) => level)
            .OrderBy(level => level.Rank)
            .Select(level => level.Score)
            .ToListAsync();

    private static EbrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<EbrDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
