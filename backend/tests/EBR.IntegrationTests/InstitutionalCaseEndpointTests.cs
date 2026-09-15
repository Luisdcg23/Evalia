using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class InstitutionalCaseEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;
    public InstitutionalCaseEndpointTests(EbrApiFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("admin@ebr.local")]
    [InlineData("coordinador@ebr.local")]
    public async Task AuthorizedRoleCreatesInstitutionalCaseWithPendingAssignmentStatus(string email)
    {
        var token = await LoginAsync(email);
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", token));

        var created = await PostJsonAsync<CaseResponse>("/api/cases/institutional", new
        {
            companyId = company.Id,
            reason = "Programación anual del plan de inspecciones",
            observations = "Incluida en el calendario institucional 2026"
        }, token);

        Assert.Equal("INSTITUTIONAL", created.SourceType);
        Assert.Equal("PENDING_ASSIGNMENT", created.Status);
        Assert.Equal(company.Id, created.CompanyId);

        var cases = await GetJsonAsync<List<CaseResponse>>("/api/cases", token);
        Assert.Contains(cases, item => item.Id == created.Id && item.SourceType == "INSTITUTIONAL");
    }

    [Fact]
    public async Task UnauthorizedRoleCannotCreateInstitutionalCase()
    {
        var token = await LoginAsync("tecnico@ebr.local");
        var adminToken = await LoginAsync("admin@ebr.local");
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", adminToken));

        using var response = await SendAsync(HttpMethod.Post, "/api/cases/institutional", new
        {
            companyId = company.Id, reason = "Intento no autorizado"
        }, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InstitutionalCaseWithoutReasonIsRejected()
    {
        var token = await LoginAsync("coordinador@ebr.local");
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", token));

        using var response = await SendAsync(HttpMethod.Post, "/api/cases/institutional", new
        {
            companyId = company.Id, reason = "   "
        }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InstitutionalCaseForUnknownCompanyIsRejected()
    {
        var token = await LoginAsync("coordinador@ebr.local");

        using var response = await SendAsync(HttpMethod.Post, "/api/cases/institutional", new
        {
            companyId = 999_999, reason = "Programación institucional"
        }, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
    private sealed record CaseResponse(int Id, int CompanyId, string SourceType, string Status);
}
