using EBR.Domain.Evaluations;

namespace EBR.UnitTests;

public sealed class BpmScoreCalculatorTests
{
    [Fact]
    public void PartialComplianceIsWorthExactlyHalfAPoint()
    {
        Assert.Equal(1m, BpmResponseOptions.ValueOf(BpmResponseOptions.Compliance));
        Assert.Equal(0.5m, BpmResponseOptions.ValueOf(BpmResponseOptions.PartialCompliance));
        Assert.Equal(0m, BpmResponseOptions.ValueOf(BpmResponseOptions.TotalNonCompliance));
        Assert.Null(BpmResponseOptions.ValueOf(BpmResponseOptions.NotApplicable));
    }

    [Fact]
    public void NotApplicableAnswersStayOutOfTheDenominator()
    {
        var result = BpmScoreCalculator.Calculate([
            new BpmAnswer("1.1.1.a", BpmResponseOptions.Compliance, 1m),
            new BpmAnswer("1.1.1.b", BpmResponseOptions.PartialCompliance, 1m),
            new BpmAnswer("1.1.1.c", BpmResponseOptions.NotApplicable, 1m)]);

        Assert.Equal(1.5m, result.Points);
        Assert.Equal(2m, result.Denominator);
        Assert.Equal(75m, result.Percentage);
        Assert.Equal(1, result.NotApplicableCount);
    }

    [Fact]
    public void TotalNonComplianceLowersThePercentageWithoutLeavingTheDenominator()
    {
        var result = BpmScoreCalculator.Calculate([
            new BpmAnswer("1.1.1.a", BpmResponseOptions.Compliance, 1m),
            new BpmAnswer("1.1.1.b", BpmResponseOptions.TotalNonCompliance, 1m)]);

        Assert.Equal(1m, result.Points);
        Assert.Equal(2m, result.Denominator);
        Assert.Equal(50m, result.Percentage);
    }

    [Fact]
    public void AnEvaluationWithoutApplicableQuestionsHasNoPercentage()
    {
        var result = BpmScoreCalculator.Calculate([
            new BpmAnswer("7.a", BpmResponseOptions.NotApplicable, 1m)]);

        Assert.Equal(0m, result.Denominator);
        Assert.Null(result.Percentage);
    }

    [Fact]
    public void UnknownResponseCodesAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            BpmScoreCalculator.Calculate([new BpmAnswer("7.a", "SI", 1m)]));
    }

    [Fact]
    public void NonPositiveWeightsAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            BpmScoreCalculator.Calculate([new BpmAnswer("7.a", BpmResponseOptions.Compliance, 0m)]));
    }
}
