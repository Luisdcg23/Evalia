namespace EBR.Domain.Evaluations;

/// <summary>
/// Niveles de criticidad de las no conformidades definidos en la guía de llenado:
/// crítica <c>C</c>, mayor <c>M</c> y menor <c>m</c> o <c>Me</c>.
/// </summary>
public static class EvaluationCriticalityLevels
{
    public const string Critical = "CRITICAL";
    public const string Major = "MAJOR";
    public const string Minor = "MINOR";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Critical, Major, Minor
    };

    /// <summary>Traduce la marca de la ficha a su código normalizado.</summary>
    public static string? FromFormMark(string? mark) => mark?.Trim() switch
    {
        null or "" => null,
        "C" => Critical,
        "M" => Major,
        "m" or "Me" => Minor,
        _ => throw new ArgumentException($"Marca de criticidad desconocida: {mark}.", nameof(mark))
    };
}

/// <summary>
/// Opciones evaluables de la ficha. <c>NA</c> no tiene valor y queda fuera del denominador.
/// </summary>
public static class BpmResponseOptions
{
    public const string Compliance = "C";
    public const string PartialCompliance = "CP";
    public const string TotalNonCompliance = "IT";
    public const string NotApplicable = "NA";

    public static readonly IReadOnlyList<(string Code, string Name, decimal? Value, bool CountsTowardDenominator)> All =
    [
        (Compliance, "Cumple", 1m, true),
        (PartialCompliance, "Cumplimiento parcial", 0.5m, true),
        (TotalNonCompliance, "Incumplimiento total", 0m, true),
        (NotApplicable, "No aplica", null, false)
    ];

    public static bool IsKnown(string code) => All.Any(option => string.Equals(option.Code, code, StringComparison.Ordinal));

    public static decimal? ValueOf(string code) => Find(code).Value;

    public static bool CountsTowardDenominator(string code) => Find(code).CountsTowardDenominator;

    private static (string Code, string Name, decimal? Value, bool CountsTowardDenominator) Find(string code)
    {
        foreach (var option in All)
        {
            if (string.Equals(option.Code, code, StringComparison.Ordinal)) return option;
        }

        throw new ArgumentException($"La opción de respuesta {code} no pertenece a la ficha BPM.", nameof(code));
    }
}

/// <summary>Respuesta individual de una pregunta con el peso que la plantilla le asigna.</summary>
public sealed record BpmAnswer(string QuestionCode, string OptionCode, decimal Weight);

/// <summary>Resultado del cálculo del porcentaje BPM.</summary>
public sealed record BpmScore(decimal Points, decimal Denominator, decimal? Percentage, int NotApplicableCount);

/// <summary>
/// Calcula el porcentaje BPM. Las respuestas <c>NA</c> no aportan puntos ni denominador;
/// una evaluación sin preguntas aplicables no tiene porcentaje y nunca se representa como cero.
/// </summary>
public static class BpmScoreCalculator
{
    public static BpmScore Calculate(IReadOnlyCollection<BpmAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var points = 0m;
        var denominator = 0m;
        var notApplicable = 0;
        foreach (var answer in answers)
        {
            if (!BpmResponseOptions.IsKnown(answer.OptionCode))
            {
                throw new ArgumentException(
                    $"La opción de respuesta {answer.OptionCode} de la pregunta {answer.QuestionCode} no pertenece a la ficha BPM.",
                    nameof(answers));
            }

            if (answer.Weight <= 0m)
            {
                throw new ArgumentException(
                    $"La pregunta {answer.QuestionCode} debe tener un peso positivo.", nameof(answers));
            }

            if (!BpmResponseOptions.CountsTowardDenominator(answer.OptionCode))
            {
                notApplicable++;
                continue;
            }

            denominator += answer.Weight;
            points += answer.Weight * BpmResponseOptions.ValueOf(answer.OptionCode)!.Value;
        }

        var percentage = denominator == 0m ? (decimal?)null : Math.Round(points / denominator * 100m, 2);
        return new BpmScore(points, denominator, percentage, notApplicable);
    }
}

/// <summary>Opción evaluable disponible para las preguntas de una versión de plantilla.</summary>
public sealed class EvaluationResponseOption
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal? Value { get; set; }
    public bool CountsTowardDenominator { get; set; } = true;
    public int Order { get; set; }
}

/// <summary>
/// Criterio de la guía de llenado asociado a un nodo de la plantilla. Conserva la referencia
/// de hoja y celda de origen para poder auditar el texto contra la ficha oficial.
/// </summary>
public sealed class EvaluationGuidanceCriterion
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public required string Code { get; set; }
    public required string Description { get; set; }
    public string? Criticality { get; set; }
    public required string SourceSheet { get; set; }
    public required string SourceCell { get; set; }
    public int Order { get; set; }
}

/// <summary>Banda de calificación de la ficha: porcentaje obtenido, clasificación y acción.</summary>
public sealed class EvaluationQualificationRule
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public required string Code { get; set; }
    public required string Description { get; set; }
    public required string Classification { get; set; }
    public required string Action { get; set; }
    public decimal? MinPercentage { get; set; }
    public bool MinIncluded { get; set; }
    public decimal? MaxPercentage { get; set; }
    public bool MaxIncluded { get; set; }
    public int Order { get; set; }
}

public static class EvaluationImportStatuses
{
    public const string Pending = "PENDIENTE";
    public const string Promoted = "PROMOVIDO";
    public const string Rejected = "RECHAZADO";
}

/// <summary>Lote de preparación de una importación de plantilla.</summary>
public sealed class EvaluationImportBatch
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string SheetName { get; set; }
    public required string ContentHash { get; set; }
    public string Status { get; set; } = EvaluationImportStatuses.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    public int? TemplateId { get; set; }
    public string? ErrorDetail { get; set; }
}

/// <summary>Fila cruda de un lote de importación de plantilla, antes de promoverse al modelo productivo.</summary>
public sealed class EvaluationImportRow
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public required string SourceCell { get; set; }
    public required string Code { get; set; }
    public string? ParentCode { get; set; }
    public required string Description { get; set; }
    public required string ItemType { get; set; }
    public int Order { get; set; }
    public string Status { get; set; } = EvaluationImportStatuses.Pending;
    public string? ErrorDetail { get; set; }
}
