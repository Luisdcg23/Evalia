namespace EBR.Domain.Companies;

public sealed class Company
{
    public int Id { get; set; }
    public required string LegalName { get; set; }
    public required string Rnc { get; set; }
    public required string TradeName { get; set; }
    public string Address { get; set; } = "";
    public string Municipality { get; set; } = "";
    public string Province { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string EconomicActivity { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public Guid VersionToken { get; set; } = Guid.NewGuid();
}
