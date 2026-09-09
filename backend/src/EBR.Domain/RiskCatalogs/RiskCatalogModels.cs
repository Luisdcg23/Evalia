namespace EBR.Domain.RiskCatalogs;

public sealed class FoodCategory
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public sealed class FoodSubcategory
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public required string Name { get; set; }
    public int? MicrobiologicalRiskLevelId { get; set; }
    public decimal? MicrobiologicalScore { get; set; }
    public int? ChemicalRiskLevelId { get; set; }
    public decimal? ChemicalScore { get; set; }
    public int? TotalRiskLevelId { get; set; }
    public decimal? TotalScore { get; set; }
}

public sealed class CompanyFoodSubcategory
{
    public int CompanyId { get; set; }
    public int SubcategoryId { get; set; }
}

public sealed class StructuralRiskFactor
{
    public int? RuleVersionId { get; set; }
    public bool IsActive { get; set; } = true;
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal Weight { get; set; }
    public int Order { get; set; }
    public ICollection<StructuralRiskOption> Options { get; } = new List<StructuralRiskOption>();
}

public sealed class StructuralRiskOption
{
    public int Id { get; set; }
    public int FactorId { get; set; }
    public required string Description { get; set; }
    public decimal Score { get; set; }
    public int Order { get; set; }
}

public sealed class CompanyRiskFactorValue
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int FactorId { get; set; }
    public int OptionId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public bool IsCurrent { get; set; }
    public Guid RegisteredBy { get; set; }
}

public sealed class InspectionFrequencyMatrix
{
    public int? RuleVersionId { get; set; }
    public int? ScaleLevelId { get; set; }
    public int Id { get; set; }
    public decimal RiskMin { get; set; }
    public bool MinimumIncluded { get; set; }
    public decimal RiskMax { get; set; }
    public int RiskLevelId { get; set; }
    public int FrequencyMonths { get; set; }
}

public sealed class RiskCalculation
{
    public int? RuleVersionId { get; set; }
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
    public decimal ProductRisk { get; set; }
    public decimal EstablishmentRisk { get; set; }
    public decimal TotalRisk { get; set; }
    public int RiskLevelId { get; set; }
    public int InspectionFrequencyMatrixId { get; set; }
    public required string FactorDetailsJson { get; set; }
    public Guid GeneratedBy { get; set; }
}
