namespace EBR.Infrastructure.Identity;

public static class UserRegistrationDocumentTypes
{
    public const string AuthorizationLetter = "CARTA_AUTORIZACION";

    public static IReadOnlyList<string> All { get; } =
    [
        AuthorizationLetter
    ];
}
