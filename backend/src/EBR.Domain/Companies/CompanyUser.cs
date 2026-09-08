namespace EBR.Domain.Companies;

public sealed class CompanyUser
{
    public int CompanyId { get; set; }
    public Guid UserId { get; set; }
}
