namespace EBR.Application.Reports;

/// <summary>
/// Firma criptográfica de documentos (RF-19, firma electrónica) con un par de claves propio del
/// sistema — sin proveedor externo ni costo. No es una firma personal por usuario: el informe oficial
/// es un documento del sistema, así que hay una única clave activa por instalación que firma todos los
/// PDF oficiales y permite verificarlos después.
/// </summary>
public interface IDocumentSigner
{
    /// <summary>Algoritmo de firma (p. ej. <c>"RSA-SHA256"</c>); queda en el metadato para poder verificar.</summary>
    string Algorithm { get; }

    /// <summary>Huella SHA-256 (hex) de la clave pública vigente; identifica qué clave firmó un documento.</summary>
    string PublicKeyThumbprint { get; }

    /// <summary>Firma el contenido con la clave privada del sistema.</summary>
    byte[] Sign(byte[] content);

    /// <summary>
    /// Verifica que <paramref name="signature"/> corresponda a <paramref name="content"/> bajo la clave
    /// pública vigente. Devuelve <c>false</c> tanto si la firma no coincide como si el contenido fue
    /// alterado después de firmarse.
    /// </summary>
    bool Verify(byte[] content, byte[] signature);

    /// <summary>Clave pública vigente en formato PEM, para que un tercero verifique sin acceso al sistema.</summary>
    string ExportPublicKeyPem();
}
