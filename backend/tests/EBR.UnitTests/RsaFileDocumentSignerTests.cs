using System.Text;
using EBR.Infrastructure.Reports;
using Microsoft.Extensions.Options;

namespace EBR.UnitTests;

/// <summary>
/// Firma electrónica del PDF oficial (RF-19) sobre RSA-2048 nativo de .NET, sin proveedor externo. La
/// clave se persiste en un archivo temporal por prueba, para no depender ni interferir con la clave
/// real de una instalación.
/// </summary>
public sealed class RsaFileDocumentSignerTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("ebr-signing-tests").FullName;

    [Fact]
    public void SignThenVerifyOfTheSameContentSucceeds()
    {
        var signer = CreateSigner();
        var content = Encoding.UTF8.GetBytes("contenido del PDF oficial");

        var signature = signer.Sign(content);

        Assert.True(signer.Verify(content, signature));
    }

    [Fact]
    public void VerifyFailsWhenTheContentWasTamperedAfterSigning()
    {
        var signer = CreateSigner();
        var original = Encoding.UTF8.GetBytes("contenido original del informe");
        var tampered = Encoding.UTF8.GetBytes("contenido alterado del informe");

        var signature = signer.Sign(original);

        Assert.False(signer.Verify(tampered, signature));
    }

    [Fact]
    public void VerifyFailsWithAnUnrelatedSignature()
    {
        var signer = CreateSigner();
        var content = Encoding.UTF8.GetBytes("documento A");
        var otherContent = Encoding.UTF8.GetBytes("documento B, completamente distinto");
        var unrelatedSignature = signer.Sign(otherContent);

        Assert.False(signer.Verify(content, unrelatedSignature));
    }

    [Fact]
    public void VerifyFailsWithMalformedSignatureBytesInsteadOfThrowing()
    {
        var signer = CreateSigner();
        var content = Encoding.UTF8.GetBytes("documento cualquiera");

        Assert.False(signer.Verify(content, [1, 2, 3]));
    }

    [Fact]
    public void TheKeyPersistsAcrossInstancesPointingAtTheSameFile()
    {
        var keyPath = Path.Combine(_tempDirectory, "signing-key.pem");
        var first = new RsaFileDocumentSigner(Options.Create(new SigningOptions { KeyPath = keyPath }));
        var content = Encoding.UTF8.GetBytes("informe firmado antes de reiniciar");
        var signature = first.Sign(content);

        // Un "reinicio" reconstruye el firmante contra el mismo archivo de clave: debe seguir
        // verificando lo que se firmó antes, no generar una clave nueva cada vez.
        var second = new RsaFileDocumentSigner(Options.Create(new SigningOptions { KeyPath = keyPath }));

        Assert.True(second.Verify(content, signature));
        Assert.Equal(first.PublicKeyThumbprint, second.PublicKeyThumbprint);
    }

    [Fact]
    public void ExportsAParseablePublicKeyPem()
    {
        var signer = CreateSigner();

        var pem = signer.ExportPublicKeyPem();

        Assert.Contains("BEGIN PUBLIC KEY", pem);
        Assert.Contains("END PUBLIC KEY", pem);
    }

    private RsaFileDocumentSigner CreateSigner() =>
        new(Options.Create(new SigningOptions { KeyPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.pem") }));

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);
}
