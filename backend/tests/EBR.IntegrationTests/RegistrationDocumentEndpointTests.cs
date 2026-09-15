using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class RegistrationDocumentEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public RegistrationDocumentEndpointTests(EbrApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task RegistrationWithoutAuthorizationLetterMetadataIsRejected()
    {
        var email = $"sin-carta-{Guid.NewGuid():N}@example.local";
        using var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Solicitante Sin Carta",
            documentNumber = "001-0000000-2",
            phoneNumber = "8095550102",
            email,
            password = "Registro2026!",
            requestedRole = "USUARIO_DELEGADO"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationWithAuthorizationLetterMetadataSucceedsAndIsPersisted()
    {
        var email = $"con-carta-{Guid.NewGuid():N}@example.local";
        using var registrationResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Solicitante Con Carta",
            documentNumber = "001-0000000-3",
            phoneNumber = "8095550103",
            email,
            password = "Registro2026!",
            requestedRole = "USUARIO_DELEGADO",
            authorizationLetterFileName = "carta-autorizacion.pdf",
            authorizationLetterMimeType = "application/pdf",
            authorizationLetterSizeBytes = 204800,
            authorizationLetterHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b85",
            authorizationLetterStorageReference = "pendientes/carta-autorizacion.pdf"
        });

        Assert.Equal(HttpStatusCode.Accepted, registrationResponse.StatusCode);

        var administrator = await LoginAsync("admin@ebr.local");
        using var pendingRequest = new HttpRequestMessage(HttpMethod.Get, "/api/users/pending");
        pendingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administrator);
        using var pendingResponse = await _client.SendAsync(pendingRequest);
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<PendingUserResponse>>();
        var user = Assert.Single(pending!, item => item.Email == email);

        var document = Assert.Single(user.RegistrationDocuments);
        Assert.Equal("CARTA_AUTORIZACION", document.DocumentType);
        Assert.Equal("carta-autorizacion.pdf", document.FileName);
        Assert.Equal("application/pdf", document.MimeType);
        Assert.Equal(204800, document.SizeBytes);
        Assert.Equal("pendientes/carta-autorizacion.pdf", document.StorageReference);
    }

    private async Task<string> LoginAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "EbrLocal2026!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private sealed record LoginResponse(string AccessToken);

    private sealed record RegistrationDocumentResponse(
        int Id,
        Guid UserId,
        string DocumentType,
        string FileName,
        string MimeType,
        long SizeBytes,
        string Hash,
        string StorageReference);

    private sealed record PendingUserResponse(
        Guid Id,
        string Email,
        IReadOnlyList<RegistrationDocumentResponse> RegistrationDocuments);
}
