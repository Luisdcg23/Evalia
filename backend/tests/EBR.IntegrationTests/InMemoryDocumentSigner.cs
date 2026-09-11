using System.Security.Cryptography;
using EBR.Application.Reports;

namespace EBR.IntegrationTests;

/// <summary>
/// Implementación de <see cref="IDocumentSigner"/> para las pruebas de integración: genera una clave
/// RSA efímera en memoria por instancia de <see cref="EbrApiFactory"/>, así que la suite no escribe ni
/// depende de un archivo de clave compartido entre fábricas que podrían correr en paralelo.
/// </summary>
public sealed class InMemoryDocumentSigner : IDocumentSigner, IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);

    public string Algorithm => "RSA-SHA256";
    public string PublicKeyThumbprint { get; }

    public InMemoryDocumentSigner()
    {
        PublicKeyThumbprint = Convert.ToHexString(SHA256.HashData(_rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }

    public byte[] Sign(byte[] content) =>
        _rsa.SignData(content, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    public bool Verify(byte[] content, byte[] signature)
    {
        try
        {
            return _rsa.VerifyData(content, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public string ExportPublicKeyPem() => _rsa.ExportSubjectPublicKeyInfoPem();

    public void Dispose() => _rsa.Dispose();
}
