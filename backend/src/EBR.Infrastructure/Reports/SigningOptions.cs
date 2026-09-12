namespace EBR.Infrastructure.Reports;

/// <summary>
/// Configuración de la firma electrónica de documentos (sección <c>Signing</c>, variables
/// <c>Signing__*</c> del archivo <c>.env</c>). A diferencia de MinIO o el correo, no hay un proveedor
/// externo que activar: la clave siempre es local y se autogenera en el primer arranque si no existe
/// todavía en <see cref="KeyPath"/>.
/// </summary>
public sealed class SigningOptions
{
    public const string SectionName = "Signing";

    /// <summary>Ruta del archivo PEM con la clave privada RSA del sistema.</summary>
    public string KeyPath { get; set; } = "storage/signing/signing-key.pem";
}
