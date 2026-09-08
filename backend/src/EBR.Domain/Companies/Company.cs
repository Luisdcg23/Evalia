namespace EBR.Domain.Companies;

public sealed class Company
{
    public int Id { get; set; }
    public required string LegalName { get; set; }
    public required string Rnc { get; set; }
    public required string TradeName { get; set; }
    public bool IsActive { get; set; } = true;
}
