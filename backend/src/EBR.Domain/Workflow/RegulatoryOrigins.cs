namespace EBR.Domain.Workflow;

public sealed class HealthAlert
{
    public int Id { get; set; }
    public required string AlertNumber { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public required string Product { get; set; }
    public int CompanyId { get; set; }
    public required string Description { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public sealed class Complaint
{
    public int Id { get; set; }
    public required string ComplaintType { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public required string Complainant { get; set; }
    public required string Description { get; set; }
    public int? CompanyId { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid CreatedBy { get; set; }
}
