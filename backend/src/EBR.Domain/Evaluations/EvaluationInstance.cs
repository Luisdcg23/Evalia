namespace EBR.Domain.Evaluations;

public static class EvaluationInstanceStatuses
{
    public const string InProgress = "IN_PROGRESS";
    public const string Submitted = "SUBMITTED";
}

/// <summary>
/// Instancia de ejecución de una evaluación (RF-12) para un expediente concreto. Vincula el caso con
/// la <see cref="EvaluationTemplate"/> publicada vigente en el momento de iniciarse: <see cref="TemplateId"/>
/// se congela al crear la instancia, así que publicar una versión posterior de la plantilla no afecta
/// evaluaciones ya en curso o enviadas. Solo puede existir una instancia por caso (ver índice único
/// en <c>EbrDbContext</c>): el flujo del caso no permite volver a <c>SCHEDULED</c> después de avanzar
/// más allá de <c>IN_EVALUATION</c>, así que en la práctica nunca hace falta una segunda.
/// </summary>
public sealed class EvaluationInstance
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public int TemplateId { get; set; }
    public Guid TemplateFamilyId { get; set; }
    public int RiskRuleVersionId { get; set; }
    public string Status { get; set; } = EvaluationInstanceStatuses.InProgress;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid StartedBy { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
}

/// <summary>
/// Fotografía inmutable del resultado de una evaluación enviada (RF-14). Guarda el porcentaje BPM con
/// su numerador y denominador, la calificación resuelta contra las bandas de la plantilla, el conteo de
/// no conformidades por severidad y el enlace al cálculo de riesgo.
/// <para>
/// <see cref="FrequencyMonths"/> se copia aquí aunque también pueda deducirse de la banda que casó en
/// el cálculo: la banda vive en <c>Matriz_Frecuencia_Inspeccion</c> y puede modificarse al publicar
/// nuevas reglas, así que leerla en el momento de la consulta haría que la frecuencia asignada a una
/// evaluación ya cerrada cambiara sola. El resto de las cifras del riesgo se leen de
/// <c>Calculo_Riesgo</c>, que sí es inmutable por disparador.
/// </para>
/// </summary>
public sealed class EvaluationResult
{
    public int Id { get; set; }
    public int EvaluationInstanceId { get; set; }
    public decimal BpmPoints { get; set; }
    public decimal BpmDenominator { get; set; }
    public decimal BpmPercentage { get; set; }
    public required string QualificationCode { get; set; }
    public required string Classification { get; set; }
    public decimal BpmRiskScore { get; set; }
    public int CriticalCount { get; set; }
    public int MajorCount { get; set; }
    public int MinorCount { get; set; }
    public int RiskCalculationId { get; set; }
    public int FrequencyMonths { get; set; }
    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EvaluationNonConformity
{
    public int Id { get; set; }
    public int EvaluationResultId { get; set; }
    public int EvaluationResponseId { get; set; }
    public int GuidanceCriterionId { get; set; }
    public required string Severity { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Respuesta capturada para una pregunta (<see cref="EvaluationTemplateItem"/> de tipo <c>QUESTION</c>)
/// dentro de una <see cref="EvaluationInstance"/>. El código de opción es uno de los catálogos C/CP/IT/NA
/// (<see cref="EvaluationResponseOption"/>) declarados para la plantilla de la instancia. Autosave
/// idempotente: guardar la misma pregunta más de una vez actualiza la fila existente en lugar de
/// duplicarla, reforzado con un índice único real en PostgreSQL sobre
/// (<see cref="EvaluationInstanceId"/>, <see cref="TemplateItemId"/>).
/// </summary>
public sealed class EvaluationResponse
{
    public int Id { get; set; }
    public int EvaluationInstanceId { get; set; }
    public int TemplateItemId { get; set; }
    public required string OptionCode { get; set; }
    public string Observations { get; set; } = "";
    public string Comments { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid SavedBy { get; set; }
}
