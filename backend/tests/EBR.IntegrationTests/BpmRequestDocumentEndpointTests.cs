using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class BpmRequestDocumentEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public BpmRequestDocumentEndpointTests(EbrApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task AddingADocumentSucceedsAndAppearsWhenQueryingTheRequest()
    {
        var token = await LoginAsync("empresa@ebr.local");
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", token));
        var draft = await PostJsonAsync<RequestResponse>("/api/bpm-requests", new
        {
            companyId = company.Id,
            establishmentType = "Procesadora de alimentos",
            reason = "Solicitud con documento",
            observations = ""
        }, token);

        Assert.Empty(draft.Documents);

        var document = await PostJsonAsync<DocumentResponse>($"/api/bpm-requests/{draft.Id}/documents", new
        {
            documentType = "Plano de instalaciones",
            fileName = "plano.pdf",
            mimeType = "application/pdf",
            sizeBytes = 51200,
            hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
            storageReference = "solicitudes/plano.pdf"
        }, token);

        Assert.Equal("Plano de instalaciones", document.DocumentType);

        var fetched = await GetJsonAsync<RequestResponse>($"/api/bpm-requests/{draft.Id}", token);
        var fetchedDocument = Assert.Single(fetched.Documents);
        Assert.Equal("plano.pdf", fetchedDocument.FileName);
    }

    [Fact]
    public async Task SubmitWithoutRequiredDocumentationIsRejected()
    {
        var token = await LoginAsync("empresa@ebr.local");
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", token));
        var draft = await PostJsonAsync<RequestResponse>("/api/bpm-requests", new
        {
            companyId = company.Id,
            establishmentType = "Almacén",
            reason = "Solicitud sin documentos",
            observations = ""
        }, token);

        using var response = await SendAsync(HttpMethod.Post, $"/api/bpm-requests/{draft.Id}/submit", new { }, token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SubmitWithAtLeastOneDocumentAttachedSucceeds()
    {
        var token = await LoginAsync("empresa@ebr.local");
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", token));
        var draft = await PostJsonAsync<RequestResponse>("/api/bpm-requests", new
        {
            companyId = company.Id,
            establishmentType = "Fábrica",
            reason = "Solicitud con documento obligatorio",
            observations = ""
        }, token);
        await PostJsonAsync<DocumentResponse>($"/api/bpm-requests/{draft.Id}/documents", new
        {
            documentType = "Documentación obligatoria",
            fileName = "documentacion.pdf",
            mimeType = "application/pdf",
            sizeBytes = 1024,
            hash = "1111111111111111111111111111111111111111111111111111111111111111",
            storageReference = "solicitudes/documentacion.pdf"
        }, token);

        var inspectionCase = await PostJsonAsync<CaseResponse>($"/api/bpm-requests/{draft.Id}/submit", new { }, token);

        Assert.Equal("PENDING_ASSIGNMENT", inspectionCase.Status);
    }

    private async Task<string> LoginAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "EbrLocal2026!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<T> GetJsonAsync<T>(string uri, string token)
    {
        using var response = await SendAsync(HttpMethod.Get, uri, null, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> PostJsonAsync<T>(string uri, object body, string token)
    {
        using var response = await SendAsync(HttpMethod.Post, uri, body, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record CompanyResponse(int Id);
    private sealed record CaseResponse(int Id, int SourceReferenceId, string Status);
    private sealed record DocumentResponse(int Id, string DocumentType, string FileName);
    private sealed record RequestResponse(int Id, string Status, IReadOnlyList<DocumentResponse> Documents);
}
