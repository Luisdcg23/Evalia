using EBR.Domain.Evaluations;
using EBR.Infrastructure.Evaluations;

namespace EBR.UnitTests;

public sealed class EvaluationSubmissionCalculatorTests
{
    /// <summary>
    /// Las bandas de calificación no se codifican en el motor: se resuelven contra las reglas que la
    /// ficha oficial dejó en la plantilla. Esta prueba las carga desde
    /// <see cref="BpmTemplateData.QualificationBands"/> (celdas D198 a D201 de la ficha) para verificar
    /// que los límites se aplican con la inclusividad declarada y sin huecos entre bandas.
    /// </summary>
    [Theory]
    [InlineData(0, "CAL-1")]
    [InlineData(60, "CAL-1")]
    [InlineData(60.01, "CAL-2")]
    [InlineData(70, "CAL-2")]
    [InlineData(70.01, "CAL-3")]
    [InlineData(75, "CAL-3")]
    [InlineData(80, "CAL-3")]
    [InlineData(80.01, "CAL-4")]
    [InlineData(100, "CAL-4")]
    public void QualificationIsResolvedFromTheBandsDeclaredByTheTemplate(double percentage, string expectedCode)
    {
        var rules = OfficialRules();

        var selected = BpmQualificationSelector.Select(rules, (decimal)percentage);

        Assert.NotNull(selected);
        Assert.Equal(expectedCode, selected.Code);
    }

    [Fact]
    public void QualificationIsUnresolvedWhenTheTemplateDeclaresNoBands()
    {
        Assert.Null(BpmQualificationSelector.Select([], 75m));
    }

    [Fact]
    public void OverlappingBandsAreRejectedInsteadOfPickingOneArbitrarily()
    {
        var rules = OfficialRules();
        rules.Add(new EvaluationQualificationRule
        {
            TemplateId = 1,
            Code = "CAL-DUP",
            Description = "Banda solapada",
            Classification = "Duplicada",
            Action = "Ninguna",
            MinPercentage = 70m,
            MinIncluded = false,
            MaxPercentage = 80m,
            MaxIncluded = true,
            Order = 5
        });

        Assert.Throws<InvalidOperationException>(() => BpmQualificationSelector.Select(rules, 75m));
    }

    private static List<EvaluationQualificationRule> OfficialRules() =>
        [.. BpmTemplateData.QualificationBands.Select((band, index) => new EvaluationQualificationRule
        {
            TemplateId = 1,
            Code = band.Code,
            Description = band.Description,
            Classification = band.Classification,
            Action = band.Action,
            MinPercentage = band.MinPercentage,
            MinIncluded = band.MinIncluded,
            MaxPercentage = band.MaxPercentage,
            MaxIncluded = band.MaxIncluded,
            Order = index + 1
        })];

    [Fact]
    public void SubmissionRequiresEveryActiveQuestionToHaveAResponse()
    {
        var questions = new[]
        {
            new EvaluationSubmissionQuestion(1, "1.a", 1m, true),
            new EvaluationSubmissionQuestion(2, "1.b", 1m, true)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EvaluationSubmissionCalculator.Calculate(questions,
                [new EvaluationSubmissionAnswer(1, "C")]));

        Assert.Contains("1.b", exception.Message);
    }

    [Fact]
    public void SubmissionCalculatesScoreAndCreatesOneNonConformityPerAffectedCriterion()
    {
        var questions = new[]
        {
            new EvaluationSubmissionQuestion(1, "1.a", 2m, true),
            new EvaluationSubmissionQuestion(2, "1.b", 1m, true),
            new EvaluationSubmissionQuestion(3, "1.c", 1m, true)
        };
        var criteria = new[]
        {
            new EvaluationSubmissionCriterion(11, 1, "1.a.1", EvaluationCriticalityLevels.Critical),
            new EvaluationSubmissionCriterion(12, 1, "1.a.2", EvaluationCriticalityLevels.Major),
            new EvaluationSubmissionCriterion(13, 2, "1.b.1", EvaluationCriticalityLevels.Minor)
        };

        var result = EvaluationSubmissionCalculator.Calculate(questions,
        [
            new EvaluationSubmissionAnswer(1, "IT"),
            new EvaluationSubmissionAnswer(2, "CP"),
            new EvaluationSubmissionAnswer(3, "NA")
        ], criteria);

        Assert.Equal(0.5m, result.Score.Points);
        Assert.Equal(3m, result.Score.Denominator);
        Assert.Equal(16.67m, result.Score.Percentage);
        Assert.Equal(3, result.NonConformities.Count);
        Assert.Equal(1, result.CriticalCount);
        Assert.Equal(1, result.MajorCount);
        Assert.Equal(1, result.MinorCount);
    }

    [Fact]
    public void NotApplicableIsRejectedWhenQuestionDoesNotAllowIt()
    {
        var questions = new[] { new EvaluationSubmissionQuestion(1, "1.a", 1m, false) };

        Assert.Throws<InvalidOperationException>(() => EvaluationSubmissionCalculator.Calculate(
            questions, [new EvaluationSubmissionAnswer(1, "NA")]));
    }
}
