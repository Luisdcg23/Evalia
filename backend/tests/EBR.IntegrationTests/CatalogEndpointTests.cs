using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class CatalogEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public CatalogEndpointTests(EbrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RiskLevelsCatalogReturnsActiveOptions()
    {
        var token = await LoginAsync("admin@ebr.local");
        var name = $"Bajo catálogo genérico {Guid.NewGuid():N}";
        using var created = await PostAuthorizedAsync("/api/catalogs/risk-levels", new
        {
            name,
            points = 97
        }, token);
        created.EnsureSuccessStatusCode();

        using var response = await GetAuthorizedAsync("/api/catalogs/NIVELES_RIESGO", token);
        var options = await response.Content.ReadFromJsonAsync<List<CatalogOptionResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Contains(options!, option => option.Name == name);
    }

    [Fact]
    public async Task SystemRolesCatalogReturnsSeededRoles()
    {
        var token = await LoginAsync("admin@ebr.local");

        using var response = await GetAuthorizedAsync("/api/catalogs/ROLES_SISTEMA", token);
        var options = await response.Content.ReadFromJsonAsync<List<CatalogOptionResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Contains(options!, option => option.Code == "ADMINISTRADOR");
        Assert.Contains(options!, option => option.Code == "COORDINADOR");
    }

    [Fact]
    public async Task UnknownCatalogCodeReturnsNotFound()
    {
        var token = await LoginAsync("admin@ebr.local");

        using var response = await GetAuthorizedAsync("/api/catalogs/CODIGO_INEXISTENTE", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<string> LoginAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "EbrLocal2026!"
        });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return login!.AccessToken;
    }

    private async Task<HttpResponseMessage> GetAuthorizedAsync(string uri, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAuthorizedAsync(string uri, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record CatalogOptionResponse(string Code, string Name);
}
