namespace EBR.Domain.Evaluations;

public static class EvaluationTemplateStatuses
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";
    public const string Retired = "RETIRED";
}

public static class EvaluationItemTypes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "CHAPTER", "SECTION", "SUBSECTION", "GROUP", "QUESTION", "INSTRUCTION", "OPTION"
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
