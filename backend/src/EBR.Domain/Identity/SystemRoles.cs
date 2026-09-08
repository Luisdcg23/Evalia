namespace EBR.Domain.Identity;

public static class SystemRoles
{
    public const string Administrator = "ADMINISTRADOR";
    public const string CompanyAdministrator = "ADMINISTRADOR_EMPRESA";
    public const string DelegateUser = "USUARIO_DELEGADO";
    public const string Coordinator = "COORDINADOR";
    public const string Evaluator = "TECNICO_EVALUADOR";

    public static IReadOnlyList<string> All { get; } =
    [
        Administrator,
        CompanyAdministrator,
        DelegateUser,
        Coordinator,
        Evaluator
    ];
}
