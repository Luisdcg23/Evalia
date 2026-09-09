namespace EBR.Infrastructure.Identity;

/// <summary>
/// Metadatos de un adjunto del registro de un usuario (RF-02). Solo se almacena la
/// referencia al archivo (nombre, tipo MIME, tamaño, hash y referencia de almacenamiento);
/// el binario nunca se guarda en PostgreSQL, lo aloja el sistema de almacenamiento de
/// evidencias correspondiente (tarea futura).
/// </summary>
public sealed class UserRegistrationDocument
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public required string DocumentType { get; set; }
    public required string FileName { get; set; }
    public required string MimeType { get; set; }
    public long SizeBytes { get; set; }
    public required string Hash { get; set; }
    public required string StorageReference { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
