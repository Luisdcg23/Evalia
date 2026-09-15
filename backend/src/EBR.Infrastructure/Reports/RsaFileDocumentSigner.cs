using System.Security.Cryptography;
using EBR.Application.Reports;
using Microsoft.Extensions.Options;

namespace EBR.Infrastructure.Reports;

/// <summary>
/// Implementación de <see cref="IDocumentSigner"/> sobre RSA-2048 (BCL, sin dependencias externas). La
/// clave privada se genera una sola vez, en el primer arranque, y se persiste en PEM bajo
/// <see cref="SigningOptions.KeyPath"/>; los arranques siguientes la reutilizan, para que los PDF
/// firmados antes se sigan verificando después de reiniciar. Pensada para una sola instancia del API:
/// dos procesos arrancando en paralelo por primera vez podrían generar claves distintas.
/// </summary>
public sealed class RsaFileDocumentSigner : IDocumentSigner, IDisposable
{
    private const int KeySizeBits = 2048;

    private readonly RSA _rsa;
    private readonly object _lock = new();

    public string Algorithm => "RSA-SHA256";
    public string PublicKeyThumbprint { get; }

    public RsaFileDocumentSigner(IOptions<SigningOptions> options)
    {
        _rsa = LoadOrCreateKey(Path.GetFullPath(options.Value.KeyPath));
        PublicKeyThumbprint = Convert.ToHexString(SHA256.HashData(_rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }

    public byte[] Sign(byte[] content)
    {
        lock (_lock)
            return _rsa.SignData(content, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public bool Verify(byte[] content, byte[] signature)
    {
        lock (_lock)
        {
            try
            {
                return _rsa.VerifyData(content, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (CryptographicException)
            {
                // Firma con formato inválido (p. ej. longitud incorrecta): no coincide, no es un error.
                return false;
            }
        }
    }

    public string ExportPublicKeyPem()
    {
        lock (_lock)
            return _rsa.ExportSubjectPublicKeyInfoPem();
    }

    private static RSA LoadOrCreateKey(string path)
    {
        if (File.Exists(path))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(path));
            return rsa;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var created = RSA.Create(KeySizeBits);
        File.WriteAllText(path, created.ExportPkcs8PrivateKeyPem());
        return created;
    }

    public void Dispose() => _rsa.Dispose();
}
