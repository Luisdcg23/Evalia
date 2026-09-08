using Microsoft.AspNetCore.Identity;

namespace EBR.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FullName { get; set; }

    public string? DocumentNumber { get; set; }

    public string? RequestedRole { get; set; }

    public string? RejectionReason { get; set; }

    public UserApprovalStatus ApprovalStatus { get; set; } = UserApprovalStatus.PendingValidation;
}
