namespace EBR.Application.Reports;

/// <summary>
/// No conformidad incluida en el PDF oficial, ya resuelta a texto: severidad normalizada, código del
/// criterio de la guía de llenado y su descripción.
/// </summary>
public sealed record OfficialReportNonConformity(string Severity, string CriterionCode, string Description);

/// <summary>Referencia de una evidencia de campo: nombre del archivo, hash SHA-256 y tamaño.</summary>
public sealed record OfficialReportEvidence(string FileName, string Sha256, long SizeBytes);

/// <summary>
/// Etiquetas de estado que se imprimen en el PDF. El oficial siempre lleva <see cref="Approved"/>
/// (solo existe tras la aprobación); la vista previa del coordinador muestra el estado que tenga la
/// versión del informe en ese momento.
/// </summary>
public static class OfficialReportStatusLabels
{
    public const string Approved = "APROBADO";
    public const string Pending = "PENDIENTE DE REVISIÓN";
    public const string Returned = "DEVUELTO";
    public const string CorrectionRequested = "CORRECCIÓN SOLICITADA";
}

/// <summary>
/// Datos ya persistidos que componen el informe oficial de una evaluación (RF-19). Todo proviene de
/// filas inmutables (<c>Evaluacion_Informe</c>, <c>Evaluacion_Resultado</c>,
/// <c>Evaluacion_No_Conformidad</c>, <c>Evaluacion_Evidencia</c>); el renderizador no calcula nada.
/// El documento lleva las dos firmas del expediente: la del técnico que lo emitió y la del
/// coordinador que lo aprobó, cada una con la rúbrica escrita y el nombre registrado de quien firmó.
/// </summary>
public sealed record OfficialReportContent(
    int EvaluationInstanceId,
    int CaseId,
    string CompanyName,
    string CompanyRnc,
    int Version,
    string ExecutiveSummary,
    string Findings,
    string Recommendations,
    decimal BpmPercentage,
    string QualificationCode,
    string Classification,
    decimal BpmRiskScore,
    int FrequencyMonths,
    int CriticalCount,
    int MajorCount,
    int MinorCount,
    IReadOnlyList<OfficialReportNonConformity> NonConformities,
    IReadOnlyList<OfficialReportEvidence> Evidences,
    DateTimeOffset ReportIssuedAt,
    DateTimeOffset GeneratedAt,
    string ApproverFullName,
    /// <summary>Rúbrica que escribió el coordinador al aprobar; se imprime en cursiva.</summary>
    string ApproverSignatureName,
    DateTimeOffset ApprovedAt,
    /// <summary>Nombre registrado del técnico que emitió la versión, como aclaración de su firma.</summary>
    string TechnicianFullName,
    /// <summary>Rúbrica que escribió el técnico al emitir; se imprime en cursiva.</summary>
    string TechnicianSignatureName,
    // Los tres campos siguientes solo afectan a la maquetación y NO forman parte del hash de contenido:
    // el PDF oficial los deja en su valor por defecto y sale como APROBADO.
    // Vista previa del coordinador: marca de agua "NO OFICIAL", sin hash ni firma electrónica.
    bool IsPreview = false,
    // Estado que se sella en el documento (ver OfficialReportStatusLabels). Nulo equivale a APROBADO.
    string? ReviewStatusLabel = null,
    // Observaciones de la revisión del coordinador, si la versión fue devuelta o con corrección solicitada.
    string? ReviewObservations = null);

/// <summary>PDF renderizado y el hash SHA-256 de su contenido lógico, estable entre generaciones.</summary>
public sealed record RenderedOfficialReport(byte[] Content, string ContentSha256);

/// <summary>
/// Genera el PDF oficial del informe a partir de datos ya persistidos. El resultado es determinista:
/// dos generaciones del mismo <see cref="OfficialReportContent"/> producen el mismo
/// <see cref="RenderedOfficialReport.ContentSha256"/> y los mismos bytes, salvo la fecha de generación
/// incrustada como metadato.
/// </summary>
public interface IOfficialReportRenderer
{
    RenderedOfficialReport Render(OfficialReportContent content);
}
