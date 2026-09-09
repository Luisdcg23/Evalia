using EBR.Application.Risk;

namespace EBR.Infrastructure.Risk;

public sealed class RiskFormulaService : IRiskFormulaService
{
    public RiskFormulaResult Calculate(RiskFormulaInput input)
    {
        if (input.ProductScores.Count == 0) throw new ArgumentException("Debe existir al menos un riesgo de producto.", nameof(input));
        if (input.Factors.Count == 0) throw new ArgumentException("Debe existir al menos un factor del establecimiento.", nameof(input));
        if (input.ProductScores.Any(score => score <= 0)) throw new ArgumentException("Los riesgos de producto deben ser positivos.", nameof(input));
        if (input.Factors.Select(factor => factor.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Factors.Count)
            throw new ArgumentException("No se permiten factores duplicados.", nameof(input));
        if (input.Factors.Any(factor => factor.Score <= 0 || factor.Weight <= 0))
            throw new ArgumentException("Los puntajes y pesos deben ser positivos.", nameof(input));
        var bands = input.FrequencyBands.OrderBy(item => item.Minimum).ToArray();
        for (var i = 0; i < bands.Length; i++)
        {
            if (bands[i].Maximum <= bands[i].Minimum || bands[i].FrequencyMonths <= 0 ||
                (i > 0 && (bands[i].Minimum < bands[i - 1].Maximum ||
                    (bands[i].Minimum == bands[i - 1].Maximum && bands[i].MinimumIncluded))))
                throw new InvalidOperationException("Los intervalos de frecuencia son inválidos o se superponen.");
        }

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
