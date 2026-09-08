namespace EBR.Application.Risk;

public interface IRiskFormulaService
{
    RiskFormulaResult Calculate(RiskFormulaInput input);
}

public sealed record RiskFactorInput(string Code, decimal Score, decimal Weight);

public sealed record RiskFrequencyBand(
    decimal Minimum,
    bool MinimumIncluded,
    decimal Maximum,
    string RiskLevel,
    int FrequencyMonths);

public sealed record RiskFormulaInput(
    IReadOnlyList<decimal> ProductScores,
    IReadOnlyList<RiskFactorInput> Factors,
    IReadOnlyList<RiskFrequencyBand> FrequencyBands);

public sealed record RiskFormulaResult(
    decimal ProductRisk,
    decimal EstablishmentRisk,
    decimal TotalRisk,
    string RiskLevel,
    int FrequencyMonths);
