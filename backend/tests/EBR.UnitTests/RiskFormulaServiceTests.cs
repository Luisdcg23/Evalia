using EBR.Application.Risk;
using EBR.Infrastructure.Risk;

namespace EBR.UnitTests;

public sealed class RiskFormulaServiceTests
{
    [Fact]
    public void CalculateUsesMaximumProductRiskWeightedEstablishmentRiskAndMatchingFrequency()
    {
        var service = new RiskFormulaService();
        var input = new RiskFormulaInput(
            [1m, 3m],
            [new("HIST", 2m, 0.4m), new("COND", 1m, 0.6m)],
            [
                new(0m, true, 3.6m, "BAJO", 12),
                new(3.6m, false, 6.3m, "MEDIO", 6),
                new(6.3m, false, 9m, "ALTO", 3)
            ]);

        var result = service.Calculate(input);

        Assert.Equal(3m, result.ProductRisk);
        Assert.Equal(1.4m, result.EstablishmentRisk);
        Assert.Equal(4.2m, result.TotalRisk);
        Assert.Equal("MEDIO", result.RiskLevel);
        Assert.Equal(6, result.FrequencyMonths);
    }

    [Theory]
    [InlineData(3.6, "BAJO")]
    [InlineData(6.3, "MEDIO")]
    [InlineData(6.31, "ALTO")]
    public void CalculateHonorsInclusiveUpperRiskBoundaries(double totalRisk, string expectedLevel)
    {
        var service = new RiskFormulaService();
        var input = new RiskFormulaInput(
            [1m],
            [new("ONLY", (decimal)totalRisk, 1m)],
            [
                new(0m, true, 3.6m, "BAJO", 12),
                new(3.6m, false, 6.3m, "MEDIO", 6),
                new(6.3m, false, 9m, "ALTO", 3)
            ]);

        var result = service.Calculate(input);

        Assert.Equal(expectedLevel, result.RiskLevel);
    }
}
