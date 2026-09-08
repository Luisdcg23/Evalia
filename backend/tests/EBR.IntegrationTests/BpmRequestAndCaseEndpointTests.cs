using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class BpmRequestAndCaseEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;
    public BpmRequestAndCaseEndpointTests(EbrApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task CompanySavesDraftAndSubmitCreatesExactlyOneCase()
    {
        var token = await LoginAsync("empresa@ebr.local");
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", token));
        var draft = await PostJsonAsync<RequestResponse>("/api/bpm-requests", new
        {
            companyId = company.Id,
            establishmentType = "Procesadora de alimentos",
            reason = "Solicitud inicial",
            observations = "Documentación en preparación"
        }, token);

        Assert.Equal("DRAFT", draft.Status);
        var firstCase = await PostJsonAsync<CaseResponse>($"/api/bpm-requests/{draft.Id}/submit", new { }, token);
        var repeatedCase = await PostJsonAsync<CaseResponse>($"/api/bpm-requests/{draft.Id}/submit", new { }, token);
        var cases = await GetJsonAsync<List<CaseResponse>>("/api/cases", await LoginAsync("coordinador@ebr.local"));

        Assert.Equal(firstCase.Id, repeatedCase.Id);
        Assert.Equal("PENDING_ASSIGNMENT", firstCase.Status);
        Assert.Single(cases, item => item.SourceReferenceId == draft.Id);
    }

    [Fact]
    public async Task CompanyCannotCreateRequestForAnUnlinkedCompany()
    {
        var admin = await LoginAsync("admin@ebr.local");
        var other = await PostJsonAsync<CompanyResponse>("/api/companies", new
        {
            legalName = "Empresa no vinculada SRL",
            rnc = $"8{Random.Shared.Next(10000000, 99999999)}",
            tradeName = "No vinculada"
        }, admin);

        using var response = await SendAsync(HttpMethod.Post, "/api/bpm-requests", new
        {
            companyId = other.Id, establishmentType = "Almacén", reason = "Prueba", observations = ""
        }, await LoginAsync("empresa@ebr.local"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidCaseTransitionIsRejectedWithoutChangingCurrentState()
    {
        var companyToken = await LoginAsync("empresa@ebr.local");
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", companyToken));
        var draft = await PostJsonAsync<RequestResponse>("/api/bpm-requests", new
        {
            companyId = company.Id, establishmentType = "Fábrica", reason = "Control", observations = ""
        }, companyToken);
        var createdCase = await PostJsonAsync<CaseResponse>($"/api/bpm-requests/{draft.Id}/submit", new { }, companyToken);

        using var invalid = await SendAsync(HttpMethod.Post, $"/api/cases/{createdCase.Id}/transition", new
        {
            newStatus = "CLOSED", reason = "No se puede cerrar antes de evaluar"
        }, await LoginAsync("coordinador@ebr.local"));
        var current = await GetJsonAsync<CaseResponse>($"/api/cases/{createdCase.Id}", await LoginAsync("coordinador@ebr.local"));

        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        Assert.Equal("PENDING_ASSIGNMENT", current.Status);
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
    private sealed record RequestResponse(int Id, string Status);
    private sealed record CaseResponse(int Id, int SourceReferenceId, string Status);
}
