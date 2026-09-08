namespace EBR.Domain.Workflow;

public static class CaseStatuses
{
    public const string PendingAssignment = "PENDING_ASSIGNMENT";
    public const string Assigned = "ASSIGNED";
    public const string Scheduled = "SCHEDULED";
    public const string InEvaluation = "IN_EVALUATION";
    public const string PendingReport = "PENDING_REPORT";
    public const string InReview = "IN_REVIEW";
    public const string CorrectionRequired = "CORRECTION_REQUIRED";
    public const string Approved = "APPROVED";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";
}

public sealed class InspectionCase
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public required string SourceType { get; set; }
    public int SourceReferenceId { get; set; }
    public string Priority { get; set; } = "NORMAL";
    public string Status { get; set; } = CaseStatuses.PendingAssignment;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; set; }
}

public sealed class CaseStateHistory
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public required string PreviousStatus { get; set; }
    public required string NewStatus { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ChangedBy { get; set; }
}

public static class CaseStateMachine
{
    private static readonly Dictionary<string, IReadOnlySet<string>> Allowed =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [CaseStatuses.PendingAssignment] = new HashSet<string>([CaseStatuses.Assigned, CaseStatuses.Cancelled], StringComparer.Ordinal),
            [CaseStatuses.Assigned] = new HashSet<string>([CaseStatuses.Scheduled, CaseStatuses.Cancelled], StringComparer.Ordinal),
            [CaseStatuses.Scheduled] = new HashSet<string>([CaseStatuses.InEvaluation, CaseStatuses.Cancelled], StringComparer.Ordinal),
            [CaseStatuses.InEvaluation] = new HashSet<string>([CaseStatuses.PendingReport], StringComparer.Ordinal),
            [CaseStatuses.PendingReport] = new HashSet<string>([CaseStatuses.InReview], StringComparer.Ordinal),
            [CaseStatuses.InReview] = new HashSet<string>([CaseStatuses.CorrectionRequired, CaseStatuses.Approved], StringComparer.Ordinal),
            [CaseStatuses.CorrectionRequired] = new HashSet<string>([CaseStatuses.InReview], StringComparer.Ordinal),
            [CaseStatuses.Approved] = new HashSet<string>([CaseStatuses.Closed], StringComparer.Ordinal)
        };

    public static bool CanTransition(string current, string target) =>
        Allowed.TryGetValue(current, out var targets) && targets.Contains(target);
}
