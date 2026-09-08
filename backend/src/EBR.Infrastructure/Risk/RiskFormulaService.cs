using EBR.Application.Risk;

namespace EBR.Infrastructure.Risk;

public sealed class RiskFormulaService : IRiskFormulaService
{
    public RiskFormulaResult Calculate(RiskFormulaInput input)
    {
        if (input.ProductScores.Count == 0) throw new ArgumentException("Debe existir al menos un riesgo de producto.", nameof(input));
        if (input.Factors.Count == 0) throw new ArgumentException("Debe existir al menos un factor del establecimiento.", nameof(input));

        var productRisk = input.ProductScores.Max();
        var establishmentRisk = input.Factors.Sum(factor => factor.Score * factor.Weight);
        var totalRisk = decimal.Round(productRisk * establishmentRisk, 3, MidpointRounding.AwayFromZero);
        var band = input.FrequencyBands
            .OrderBy(item => item.Minimum)
            .FirstOrDefault(item =>
                (item.MinimumIncluded ? totalRisk >= item.Minimum : totalRisk > item.Minimum) &&
                totalRisk <= item.Maximum)
            ?? throw new InvalidOperationException("El riesgo total no pertenece a ningún intervalo de frecuencia.");

        return new RiskFormulaResult(productRisk, establishmentRisk, totalRisk, band.RiskLevel, band.FrequencyMonths);
    }
}
