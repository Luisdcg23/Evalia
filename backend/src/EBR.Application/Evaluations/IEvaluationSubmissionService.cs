namespace EBR.Application.Evaluations;

/// <summary>
/// Consolidación del resultado de una evaluación BPM enviada (RF-14): porcentaje de cumplimiento,
/// no conformidades por severidad y cálculo de riesgo/frecuencia de inspección asociado.
/// </summary>
public interface IEvaluationSubmissionService
{
    /// <summary>
    /// Comprueba que la evaluación pueda calificarse antes de bloquearla. Devuelve <c>null</c> si puede
    /// enviarse, o el motivo del rechazo si no. Se ejecuta antes del envío para que una evaluación que
    /// no puede calificarse quede intacta y en <c>IN_PROGRESS</c>.
    /// </summary>
    Task<string?> DescribeSubmissionBlockerAsync(int evaluationInstanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Calcula y persiste el resultado inmutable de una evaluación ya enviada. Solo debe llamarse
    /// después de que la instancia quede en <c>SUBMITTED</c>.
    /// </summary>
    Task<EvaluationSubmissionOutcome> RegisterResultAsync(
        int evaluationInstanceId,
        Guid submittedBy,
        CancellationToken cancellationToken);
}

public sealed record EvaluationSubmissionOutcome(
    int EvaluationInstanceId,
    int EvaluationResultId,
    decimal BpmPercentage,
    string QualificationCode,
    string Classification,
    int CriticalCount,
    int MajorCount,
    int MinorCount,
    int RiskCalculationId);
