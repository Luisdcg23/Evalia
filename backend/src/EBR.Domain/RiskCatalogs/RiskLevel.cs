namespace EBR.Domain.RiskCatalogs;

public sealed class RiskLevel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Points { get; set; }
}
