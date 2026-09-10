namespace EBR.Domain.Evaluations;

/// <summary>
/// Decisiones del coordinador al revisar el informe (RF-17). El SRS enumera tres acciones —aprobar,
/// devolver y solicitar corrección— sin definir en qué se diferencian las dos últimas, así que se
/// conservan como decisiones distintas para registrar la intención del coordinador, y ambas dejan el
/// expediente en el mismo estado <c>CORRECTION_REQUIRED</c>, que es el único que la máquina de
/// estados contempla para la devolución.
/// </summary>
public static class EvaluationReportDecisions
{
    public const string Approved = "APPROVED";
    public const string Returned = "RETURNED";
    public const string CorrectionRequested = "CORRECTION_REQUESTED";

    public static IReadOnlyList<string> All { get; } = [Approved, Returned, CorrectionRequested];
}

public static class EvaluationReportStatuses
{
    /// <summary>Emitido por el técnico y a la espera de la revisión del coordinador.</summary>
    public const string Issued = "ISSUED";

    /// <summary>Revisado un informe, su estado es la decisión que tomó el coordinador.</summary>
    public static IReadOnlyList<string> All { get; } = [Issued, .. EvaluationReportDecisions.All];
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

/// <summary>
/// Revisión del coordinador sobre una versión concreta del informe (RF-17). Se guarda una fila por
/// decisión y ninguna se reemplaza: las observaciones son lo que el técnico consulta para corregir
/// (RF-18), así que el historial de la revisión forma parte del expediente igual que el informe.
/// </summary>
public sealed class EvaluationReportReview
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public required string Decision { get; set; }
    public required string Observations { get; set; }
    public DateTimeOffset ReviewedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ReviewedBy { get; set; }
}

/// <summary>Metadatos inmutables del PDF oficial; el binario permanece en almacenamiento de objetos.</summary>
public sealed class EvaluationOfficialReport
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public required string FileName { get; set; }
    public string MimeType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public required string StorageKey { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid GeneratedBy { get; set; }
}

/// <summary>Acta inmutable del cierre de un expediente contra un informe oficial aprobado.</summary>
public sealed class CaseClosure
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public int ReportId { get; set; }
    public int OfficialReportId { get; set; }
    public string Status { get; set; } = "CLOSED";
    public required string Result { get; set; }
    public DateTimeOffset ClosedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ClosedBy { get; set; }
}
