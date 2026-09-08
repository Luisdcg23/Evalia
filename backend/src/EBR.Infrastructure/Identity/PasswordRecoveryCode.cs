namespace EBR.Infrastructure.Identity;

public sealed class PasswordRecoveryCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
