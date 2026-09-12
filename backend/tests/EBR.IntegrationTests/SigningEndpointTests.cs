using System.Net.Http.Json;

namespace EBR.IntegrationTests;

/// <summary>
/// Clave pública del sistema (RF-19). Es información no sensible y de acceso anónimo por diseño: un
/// tercero debe poder verificar la firma de un PDF oficial sin cuenta ni acceso al sistema.
/// </summary>
public sealed class SigningEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public SigningEndpointTests(EbrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ThePublicKeyIsServedAnonymouslyAsAParseablePem()
    {
        using var response = await _client.GetAsync("/api/signing/public-key");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PublicKeyResponse>();

        Assert.NotNull(body);
        Assert.Equal("RSA-SHA256", body!.Algorithm);
        Assert.Equal(64, body.PublicKeyThumbprint.Length);
        Assert.Contains("BEGIN PUBLIC KEY", body.PublicKeyPem);
    }

    private sealed record PublicKeyResponse(string Algorithm, string PublicKeyThumbprint, string PublicKeyPem);
}
