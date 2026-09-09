namespace EBR.Domain.Companies;

public static class CompanyRepresentativeTypes
{
    public const string Legal = "LEGAL";
    public const string Quality = "CALIDAD";
    public const string PrimaryContact = "CONTACTO_PRINCIPAL";

    public static IReadOnlyList<string> All { get; } =
    [
        Legal,
        Quality,
        PrimaryContact
    ];
}
