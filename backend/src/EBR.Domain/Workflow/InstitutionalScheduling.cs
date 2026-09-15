namespace EBR.Domain.Workflow;

public sealed class InstitutionalScheduling
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public required string Reason { get; set; }
    public string Observations { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; set; }
}
