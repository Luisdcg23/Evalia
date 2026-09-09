namespace EBR.Domain.Workflow;

/// <summary>
/// Metadatos de un documento de la documentación obligatoria de una <see cref="BpmRequest"/>
/// (RF-05). Como en <c>UserRegistrationDocument</c>, solo se guarda la referencia al archivo;
/// el binario no se persiste en PostgreSQL.
/// </summary>
public sealed class BpmRequestDocument
{
    public int Id { get; set; }
    public int BpmRequestId { get; set; }
    public required string DocumentType { get; set; }
    public required string FileName { get; set; }
    public required string MimeType { get; set; }
    public long SizeBytes { get; set; }
    public required string Hash { get; set; }
    public required string StorageReference { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
