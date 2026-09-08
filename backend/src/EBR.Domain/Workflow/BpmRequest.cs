namespace EBR.Domain.Workflow;

public static class BpmRequestStatuses
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
}

public sealed class BpmRequest
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public required string EstablishmentType { get; set; }
    public required string Reason { get; set; }
    public string Observations { get; set; } = "";
    public string Status { get; set; } = BpmRequestStatuses.Draft;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid VersionToken { get; set; } = Guid.NewGuid();
}
