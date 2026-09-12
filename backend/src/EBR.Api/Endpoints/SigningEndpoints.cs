using EBR.Application.Reports;

namespace EBR.Api.Endpoints;

/// <summary>
/// Clave pública del sistema para la firma electrónica del PDF oficial (RF-19). Es información no
/// sensible por diseño —lo contrario de la clave privada— y su propósito es justamente permitir que un
/// tercero verifique la firma de un documento sin necesidad de una cuenta ni acceso al sistema.
/// </summary>
public static class SigningEndpoints
{
    public static IEndpointRouteBuilder MapSigningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/signing/public-key", GetPublicKeyAsync)
            .WithTags("Firma electrónica")
            .AllowAnonymous();
        return endpoints;
    }

    private static IResult GetPublicKeyAsync(IDocumentSigner documentSigner) =>
        Results.Ok(new PublicKeyResponse(documentSigner.Algorithm, documentSigner.PublicKeyThumbprint, documentSigner.ExportPublicKeyPem()));

    private sealed record PublicKeyResponse(string Algorithm, string PublicKeyThumbprint, string PublicKeyPem);
}
