namespace EBR.Domain.Companies;

/// <summary>
/// Fila de auditoría de una actualización de <see cref="Company"/>. Es append-only: la invariante
/// se refuerza en <c>EbrDbContext.SaveChanges</c> y se duplica en PostgreSQL con el disparador
/// <c>tr_empresa_historial_inmutable</c>.
/// </summary>
public sealed class CompanyHistoryEntry
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ChangedBy { get; set; }
    public required string ChangesJson { get; set; }
}
