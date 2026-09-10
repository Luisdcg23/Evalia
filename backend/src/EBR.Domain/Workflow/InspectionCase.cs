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

    public static IReadOnlyList<string> All { get; } =
    [
        PendingAssignment,
        Assigned,
        Scheduled,
        InEvaluation,
        PendingReport,
        InReview,
        CorrectionRequired,
        Approved,
        Closed,
        Cancelled
    ];
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

    /// <summary>
    /// Estados en los que reasignar el técnico de un caso ya asignado tiene sentido: mientras el
    /// expediente sigue activo y el técnico aún tiene trabajo pendiente sobre él (ejecutar,
    /// programar, evaluar, reportar) o su informe todavía puede corregirse. A partir de
    /// <see cref="CaseStatuses.Approved"/> el informe ya quedó aprobado y reasignar el técnico no
    /// tiene efecto útil; los estados terminales (<see cref="CaseStatuses.Closed"/>,
    /// <see cref="CaseStatuses.Cancelled"/>) tampoco admiten reasignación.
    /// </summary>
    private static readonly HashSet<string> ReassignableStatuses = new HashSet<string>(
        [
            CaseStatuses.Assigned,
            CaseStatuses.Scheduled,
            CaseStatuses.InEvaluation,
            CaseStatuses.PendingReport,
            CaseStatuses.InReview,
            CaseStatuses.CorrectionRequired
        ],
        StringComparer.Ordinal);

    public static bool CanTransition(string current, string target) =>
        Allowed.TryGetValue(current, out var targets) && targets.Contains(target);

    public static bool CanReassign(string current) => ReassignableStatuses.Contains(current);
}

/// <summary>
/// Asignación de un técnico evaluador a un caso. Sigue el patrón <c>IsCurrent</c> de
/// <see cref="EBR.Domain.RiskCatalogs.CompanyRiskFactorValue"/>: solo una fila por caso puede tener
/// <see cref="IsCurrent"/> en verdadero a la vez; las anteriores quedan como historial y nunca se
/// eliminan.
/// </summary>
public sealed class CaseAssignment
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public Guid TechnicianId { get; set; }
    public bool IsCurrent { get; set; } = true;
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid AssignedBy { get; set; }
    public required string Reason { get; set; }
}

/// <summary>
/// Ventana de tiempo que reserva una evaluación en la agenda del técnico, usada para detectar
/// solapamientos (RF-07/RF-11). El SRS no especifica una duración explícita por evaluación; se fija
/// aquí una ventana de 2 horas por evaluación como estimación operativa razonable de una visita de
/// inspección BPM (recorrido del establecimiento y llenado de la ficha). Documentado también en
/// DATABASE.md.
/// </summary>
public static class CaseScheduleWindow
{
    public const int HoursPerEvaluation = 2;
}

/// <summary>
/// Programación (agenda) de la evaluación de un caso (RF-07/RF-11). Sigue el mismo patrón
/// <see cref="CaseAssignment.IsCurrent"/>: programar por primera vez o reprogramar crea una nueva
/// fila vigente y marca la anterior como histórica; ninguna fila se elimina. Cancelar es distinto:
/// no hay una fecha nueva que la reemplace, así que solo se apaga <see cref="IsCurrent"/> de la fila
/// vigente y se registra quién y cuándo canceló (<see cref="CancelledAt"/>, <see cref="CancelledBy"/>,
/// <see cref="CancellationReason"/>), dejando constancia explícita de la cancelación en la misma fila
/// histórica en lugar de solo un cambio de estado del caso.
/// </summary>
public sealed class CaseSchedule
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public string Priority { get; set; } = "NORMAL";
    public required string Reason { get; set; }
    public string Observations { get; set; } = "";
    public bool IsCurrent { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ScheduledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}
