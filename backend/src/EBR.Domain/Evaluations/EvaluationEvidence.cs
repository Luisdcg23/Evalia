namespace EBR.Domain.Evaluations;

/// <summary>
/// Evidencia adjunta a una evaluación en campo. El binario vive en el almacenamiento de objetos y en
/// la base solo quedan los metadatos: nombre original, tipo MIME, tamaño, hash SHA-256 y clave del
/// objeto. La evidencia se asocia siempre a la instancia y, cuando el técnico la toma respondiendo una
/// pregunta concreta, también a esa respuesta (<see cref="EvaluationResponseId"/>).
/// </summary>
public sealed class EvaluationEvidence
{
    public int Id { get; set; }
    public int EvaluationInstanceId { get; set; }
    public int? EvaluationResponseId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid UploadedBy { get; set; }
}

/// <summary>
/// Reglas de admisión de evidencias. Son deliberadamente cerradas: la evidencia sustenta un informe
/// oficial, así que solo se aceptan formatos que puedan mostrarse e imprimirse sin intérprete externo
/// (fotografías y PDF). El mismo juego de valores se replica como restricción de comprobación en
/// PostgreSQL para que un cliente distinto de la PWA tampoco pueda insertar otro tipo.
/// </summary>
public static class EvidencePolicy
{
    public const long MaxSizeBytes = 15L * 1024 * 1024;

    public static readonly IReadOnlyList<string> AllowedMimeTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    ];

    public static bool IsAllowed(string mimeType) =>
        AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
}
