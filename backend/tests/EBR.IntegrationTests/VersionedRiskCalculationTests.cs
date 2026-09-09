using EBR.Application.Risk;
using EBR.Domain.Companies;
using EBR.Domain.RiskCatalogs;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.EntityFrameworkCore;

namespace EBR.IntegrationTests;

public sealed class VersionedRiskCalculationTests
{
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 8)]
    public async Task ProductScaleUsesMaximumKnownHazardAndFrequencyUsesIndependentScale(bool chemical, int expected)
    {
        await using var context = await SeedAsync();
        if (chemical)
        {
            context.FoodSubcategoryHazards.Add(new() { SubcategoryId = 1, HazardType = "CHEMICAL", LevelId = 3 });
            await context.SaveChangesAsync();
        }
        var result = await CalculateAsync(context);
        Assert.Equal(expected, result.ProductRisk);
        Assert.Equal(expected, result.TotalRisk);
        Assert.Equal(chemical ? 3 : 6, result.FrequencyMonths);
        Assert.Equal(chemical ? 3 : 2, result.RiskLevelId);
        Assert.Equal(chemical ? 2 : 1, await context.FoodSubcategoryHazards.CountAsync());
        Assert.Null((await context.FoodSubcategories.SingleAsync()).ChemicalScore);
    }

    [Fact]
    public async Task CalculationPreservesRuleAndSnapshotAfterHazardChanges()
    {
        await using var context = await SeedAsync();
        var result = await CalculateAsync(context);
        var calculation = await context.RiskCalculations.SingleAsync();
        Assert.Equal(1, calculation.RuleVersionId);
        Assert.Contains("ruleVersion", calculation.FactorDetailsJson);
        var snapshot = calculation.FactorDetailsJson;
        (await context.FoodSubcategoryHazards.SingleAsync()).ScoreOverride = 8m;
        await context.SaveChangesAsync();
        Assert.Equal(snapshot, (await context.RiskCalculations.AsNoTracking().SingleAsync()).FactorDetailsJson);
        Assert.Equal(4m, result.ProductRisk);
        calculation.FactorDetailsJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task MissingFactorDoesNotPersistPartialCalculation()
    {
        await using var context = await SeedAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => CalculateAsync(context, [new(1, 1)]));
        Assert.Empty(await context.RiskCalculations.ToArrayAsync());
        Assert.Empty(await context.CompanyRiskFactorValues.ToArrayAsync());
    }

    [Fact]
    public async Task DuplicateSelectionIsRejectedBeforeWriting()
    {
        await using var context = await SeedAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => CalculateAsync(context, [new(1, 1), new(1, 1)]));
        Assert.Empty(await context.RiskCalculations.ToArrayAsync());
    }

    [Fact]
    public async Task UnpublishedRuleCannotBeUsed()
    {
        await using var context = await SeedAsync(published: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CalculateAsync(context));
    }

    [Fact]
    public async Task PublishedVersionMustHaveSixActiveFactorsWithExactlyOneTotalWeight()
    {
        await using var context = await SeedAsync(published: false);
        (await context.StructuralRiskFactors.FirstAsync()).Weight = .1601m;
        (await context.RiskRuleVersions.SingleAsync()).IsPublished = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task OptionFromDifferentFactorCannotBeSelected()
    {
        await using var context = await SeedAsync();
        var selections = Enumerable.Range(1, 6).Select(id => new RiskFactorSelection(id, id == 1 ? 2 : id)).ToArray();
        await Assert.ThrowsAsync<ArgumentException>(() => CalculateAsync(context, selections));
    }

    [Fact]
    public async Task MissingHazardsCannotFallBackToLegacyTotalScore()
    {
        await using var context = await SeedAsync();
        context.FoodSubcategoryHazards.RemoveRange(context.FoodSubcategoryHazards);
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => CalculateAsync(context));
    }

    private static async Task<RiskCalculationResult> CalculateAsync(EbrDbContext context, IReadOnlyList<RiskFactorSelection>? selections = null) =>
        await new RiskCalculationService(context, new RiskFormulaService()).CalculateAsync(
            new(1, selections ?? Enumerable.Range(1, 6).Select(id => new RiskFactorSelection(id, id)).ToArray(),
                (await context.Users.SingleAsync()).Id), CancellationToken.None);

    internal static async Task<EbrDbContext> SeedAsync(bool published = true)
    {
        var context = new EbrDbContext(new DbContextOptionsBuilder<EbrDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), UserName = "risk-test", FullName = "Risk Test" });
        context.Companies.Add(new Company { Id = 1, LegalName = "Test", Rnc = "123456789", TradeName = "Test" });
        context.FoodCategories.Add(new() { Id = 1, Name = new string('C', 195) });
        context.FoodSubcategories.Add(new() { Id = 1, CategoryId = 1, Name = new string('S', 209), TotalScore = 2m });
        context.CompanyFoodSubcategories.Add(new() { CompanyId = 1, SubcategoryId = 1 });
        var codes = new[] { "LOW", "MEDIUM", "HIGH" };
        decimal[] scores = [2m, 4m, 8m], minima = [1m, 3.6m, 6.3m], maxima = [3.6m, 6.3m, 99999.999m];
        int[] months = [12, 6, 3];
        context.RiskScales.AddRange(
            new RiskScale { Id = 1, Code = "PRODUCT", Name = "Producto", Version = 1, EffectiveFrom = DateTimeOffset.UnixEpoch },
            new RiskScale { Id = 2, Code = "FREQUENCY", Name = "Frecuencia", Version = 1, EffectiveFrom = DateTimeOffset.UnixEpoch });
        for (var i = 0; i < 3; i++)
        {
            context.RiskLevels.Add(new() { Id = i + 1, Name = codes[i], Points = i + 1 });
            context.RiskScaleLevels.Add(new() { Id = i + 1, ScaleId = 1, Code = codes[i], Name = codes[i], Rank = i + 1, Score = scores[i] });
            context.RiskScaleLevels.Add(new() { Id = i + 4, ScaleId = 2, Code = codes[i], Name = codes[i], Rank = i + 1, Score = i + 1 });
            context.InspectionFrequencyMatrices.Add(new() { Id = i + 1, RuleVersionId = 1, ScaleLevelId = i + 4, RiskLevelId = i + 1,
                RiskMin = minima[i], MinimumIncluded = i == 0, RiskMax = maxima[i], FrequencyMonths = months[i] });
        }
        context.RiskRuleVersions.Add(new() { Id = 1, Version = 1, ProductScaleId = 1, FrequencyScaleId = 2,
            EffectiveFrom = DateTimeOffset.UnixEpoch, IsPublished = published });
        context.FoodSubcategoryHazards.Add(new() { SubcategoryId = 1, HazardType = "MICROBIOLOGICAL", LevelId = 2 });
        var weights = new[] { .16m, .09m, .56m, .05m, .06m, .08m };
        for (var i = 0; i < 6; i++)
        {
            context.StructuralRiskFactors.Add(new() { Id = i + 1, Code = $"FACTOR{i}", Name = $"Factor {i}", RuleVersionId = 1, Weight = weights[i], Order = i });
            context.StructuralRiskOptions.Add(new() { Id = i + 1, FactorId = i + 1, Description = "Bajo", Score = 1m, Order = 1 });
        }
        await context.SaveChangesAsync();
        return context;
    }
}
