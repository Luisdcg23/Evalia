namespace EBR.Domain.Evaluations;

public static class EvaluationTemplateStatuses
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";
    public const string Retired = "RETIRED";
}

public static class EvaluationItemTypes
{
    public const string Question = "QUESTION";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "CHAPTER", "SECTION", "SUBSECTION", "GROUP", Question, "INSTRUCTION", "OPTION"
    };
}

public sealed class EvaluationTemplate
{
    public int Id { get; set; }
    public Guid FamilyId { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = EvaluationTemplateStatuses.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>
/// Fila única que declara cuál <see cref="EvaluationTemplate"/> usa
/// <c>POST /api/cases/{id}/evaluations</c> para iniciar evaluaciones nuevas. Deliberadamente separada
/// de <see cref="EvaluationTemplate"/> (tabla <c>Plantilla_Activa</c>, sin disparador de inmutabilidad):
/// una plantilla publicada es inmutable en su contenido, pero cuál de las plantillas publicadas está
/// activa debe poder cambiar (por ejemplo, al corregir un error de activación) sin violar esa
/// inmutabilidad. La tabla admite como máximo una fila (<c>CK_Plantilla_Activa_Singleton</c>,
/// <see cref="Id"/> siempre <see cref="SingletonId"/>): "la plantilla activa" es un concepto singular
/// de todo el sistema, no por empresa ni por caso (el SRS no define esa segmentación).
/// </summary>
public sealed class ActiveEvaluationTemplate
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public int TemplateId { get; set; }
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Quién activó la plantilla. Nulo cuando la fila la sembró la migración de datos (activación
    /// automática de la ficha oficial al desplegar el esquema, no un acto de un usuario del sistema).
    /// </summary>
    public Guid? ActivatedBy { get; set; }
}

public sealed class EvaluationTemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int? ParentId { get; set; }
    public required string Code { get; set; }
    public required string Description { get; set; }
    public required string ItemType { get; set; }
    public int Order { get; set; }
    public decimal? Weight { get; set; }
    public bool IsRequired { get; set; }
    public bool IsCritical { get; set; }
    public bool AllowsNotApplicable { get; set; }
    public string? ResponseType { get; set; }
    public string RulesJson { get; set; } = "{}";
    public string ScoreConfigurationJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public Guid VersionToken { get; set; } = Guid.NewGuid();
}
