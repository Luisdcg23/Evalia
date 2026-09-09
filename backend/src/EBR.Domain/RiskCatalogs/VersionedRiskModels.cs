namespace EBR.Domain.RiskCatalogs;

public sealed class RiskScale
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int Version { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RiskScaleLevel> Levels { get; set; } = new List<RiskScaleLevel>();
}

public sealed class RiskScaleLevel
{
    public int Id { get; set; }
    public int ScaleId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal Score { get; set; }
    public int Rank { get; set; }
}

public sealed class FoodSubcategoryHazard
{
    public int Id { get; set; }
    public int SubcategoryId { get; set; }
    public required string HazardType { get; set; }
    public int LevelId { get; set; }
    public decimal? ScoreOverride { get; set; }
}

public sealed class RiskRuleVersion
{
    public int Id { get; set; }
    public int Version { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; }
    public string AggregationMethod { get; set; } = "MAX";
    public int ProductScaleId { get; set; }
    public int FrequencyScaleId { get; set; }
}
