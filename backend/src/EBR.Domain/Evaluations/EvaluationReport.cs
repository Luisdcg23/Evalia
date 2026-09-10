namespace EBR.Domain.Evaluations;

public static class EvaluationReportStatuses
{
    /// <summary>Emitido por el técnico y a la espera de la revisión del coordinador.</summary>
    public const string Issued = "ISSUED";

    public static IReadOnlyList<string> All { get; } = [Issued];
}

/// <summary>
/// Informe de una evaluación enviada (RF-16). Se versiona: emitir de nuevo tras una corrección crea
/// la versión siguiente y conserva intactas las anteriores, porque el informe es lo que se comunica
/// al establecimiento y una corrección no puede borrar lo ya emitido. Las cifras (porcentaje BPM,
/// calificación, no conformidades) no se copian aquí: viven en <see cref="EvaluationResult"/>, que
/// ya es inmutable, así que cada versión del informe las reproduce sin riesgo de divergir.
/// </summary>
public sealed class EvaluationReport
{
    public int Id { get; set; }
    public int EvaluationInstanceId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = EvaluationReportStatuses.Issued;
    public required string ExecutiveSummary { get; set; }
    public required string Findings { get; set; }
    public required string Recommendations { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; set; }
}
