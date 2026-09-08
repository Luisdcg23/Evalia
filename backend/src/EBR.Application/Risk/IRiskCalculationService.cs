namespace EBR.Application.Risk;

public interface IRiskCalculationService
{
    Task<RiskCalculationResult> CalculateAsync(RiskCalculationCommand command, CancellationToken cancellationToken);
}

public sealed record RiskFactorSelection(int FactorId, int OptionId);

public sealed record RiskCalculationCommand(
    int CompanyId,
    IReadOnlyList<RiskFactorSelection> FactorSelections,
    Guid GeneratedBy);

public sealed record RiskCalculationResult(
    int Id,
    int CompanyId,
    DateTimeOffset CalculatedAt,
    decimal ProductRisk,
    decimal EstablishmentRisk,
    decimal TotalRisk,
    int RiskLevelId,
    int FrequencyMonths,
    DateTimeOffset NextInspectionAt,
    string FactorDetailsJson);
