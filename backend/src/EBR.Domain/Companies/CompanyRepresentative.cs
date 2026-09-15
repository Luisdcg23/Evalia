namespace EBR.Domain.Companies;

public sealed class CompanyRepresentative
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public required string FullName { get; set; }
    public required string DocumentNumber { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public required string RepresentativeType { get; set; }
    public bool IsActive { get; set; } = true;
}
