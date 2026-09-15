namespace EBR.Domain.Workflow;

public static class NotificationTypes
{
    public const string CaseAssigned = "CASE_ASSIGNED";
    public const string CaseScheduled = "CASE_SCHEDULED";
    public const string ReportCorrectionRequested = "REPORT_CORRECTION_REQUESTED";
    public const string ReportApproved = "REPORT_APPROVED";
    public const string CaseClosed = "CASE_CLOSED";
}

/// <summary>Notificación persistida y privada para un usuario del sistema.</summary>
public sealed class Notification
{
    public int Id { get; set; }
    public Guid RecipientId { get; set; }
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? OperationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
