using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class CompanyProfileEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public CompanyProfileEndpointTests(EbrApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task UpdatingWithCurrentVersionTokenSucceedsAndAppendsHistoryEntry()
    {
        var token = await LoginAsync("admin@ebr.local");
        var company = await CreateCompanyAsync(token);

        var updated = await PutJsonAsync<CompanyResponse>($"/api/companies/{company.Id}", new
        {
            legalName = company.LegalName,
            tradeName = company.TradeName,
            address = "Av. Winston Churchill 100",
            municipality = "Santo Domingo de Guzmán",
            province = "Distrito Nacional",
            phoneNumber = "8095551234",
            email = "contacto@empresa.local",
            economicActivity = "Procesamiento de alimentos",
            versionToken = company.VersionToken
        }, token);

        Assert.Equal("Av. Winston Churchill 100", updated.Address);
        Assert.NotEqual(company.VersionToken, updated.VersionToken);

        var history = await GetJsonAsync<List<HistoryEntryResponse>>($"/api/companies/{company.Id}/history", token);
        var entry = Assert.Single(history);
        Assert.Contains("address", entry.ChangesJson);
        Assert.Contains("Av. Winston Churchill 100", entry.ChangesJson);
    }

    [Fact]
    public async Task UpdatingWithStaleVersionTokenIsRejectedWithoutChangesOrHistory()
    {
        var token = await LoginAsync("admin@ebr.local");
        var company = await CreateCompanyAsync(token);
        var staleToken = company.VersionToken;

        using var first = await SendAsync(HttpMethod.Put, $"/api/companies/{company.Id}", new
        {
            legalName = company.LegalName,
            tradeName = company.TradeName,
            address = "Primera dirección",
            municipality = "Santiago",
            province = "Santiago",
            phoneNumber = "8095550001",
            email = "primero@empresa.local",
            economicActivity = "Distribución",
            versionToken = staleToken
        }, token);
        first.EnsureSuccessStatusCode();
        var afterFirst = (await first.Content.ReadFromJsonAsync<CompanyResponse>())!;

        using var second = await SendAsync(HttpMethod.Put, $"/api/companies/{company.Id}", new
        {
            legalName = company.LegalName,
            tradeName = company.TradeName,
            address = "Segunda dirección intentada",
            municipality = "La Vega",
            province = "La Vega",
            phoneNumber = "8095550002",
            email = "segundo@empresa.local",
            economicActivity = "Manufactura",
            versionToken = staleToken
        }, token);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var current = await GetJsonAsync<CompanyResponse>($"/api/companies/{company.Id}", token);
        Assert.Equal("Primera dirección", current.Address);
        Assert.Equal(afterFirst.VersionToken, current.VersionToken);

        var history = await GetJsonAsync<List<HistoryEntryResponse>>($"/api/companies/{company.Id}/history", token);
        Assert.Single(history);
    }

    [Fact]
    public async Task UpdateRejectsBlankRequiredFields()
    {
        var token = await LoginAsync("admin@ebr.local");
        var company = await CreateCompanyAsync(token);

        using var response = await SendAsync(HttpMethod.Put, $"/api/companies/{company.Id}", new
        {
            legalName = company.LegalName,
            tradeName = company.TradeName,
            address = "",
            municipality = "Santiago",
            province = "Santiago",
            phoneNumber = "8095550001",
            email = "correo-invalido",
            economicActivity = "Distribución",
            versionToken = company.VersionToken
        }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var history = await GetJsonAsync<List<HistoryEntryResponse>>($"/api/companies/{company.Id}/history", token);
        Assert.Empty(history);
    }

    [Fact]
    public async Task SecondActiveRepresentativeOfSameTypeIsRejected()
    {
        var token = await LoginAsync("admin@ebr.local");
        var company = await CreateCompanyAsync(token);

        using var firstRepresentative = await SendAsync(HttpMethod.Post, $"/api/companies/{company.Id}/representatives", new
        {
            fullName = "Representante Legal Uno",
            documentNumber = "001-0000001-1",
            email = "legal1@empresa.local",
            phoneNumber = "8095550011",
            representativeType = "LEGAL"
        }, token);
        firstRepresentative.EnsureSuccessStatusCode();

        using var secondRepresentative = await SendAsync(HttpMethod.Post, $"/api/companies/{company.Id}/representatives", new
        {
            fullName = "Representante Legal Dos",
            documentNumber = "001-0000002-2",
            email = "legal2@empresa.local",
            phoneNumber = "8095550012",
            representativeType = "LEGAL"
        }, token);

        Assert.Equal(HttpStatusCode.Conflict, secondRepresentative.StatusCode);

        using var differentTypeRepresentative = await SendAsync(HttpMethod.Post, $"/api/companies/{company.Id}/representatives", new
        {
            fullName = "Representante Calidad",
            documentNumber = "001-0000003-3",
            email = "calidad@empresa.local",
            phoneNumber = "8095550013",
            representativeType = "CALIDAD"
        }, token);

        Assert.Equal(HttpStatusCode.Created, differentTypeRepresentative.StatusCode);
    }

    [Fact]
    public async Task RepresentativeWithUnknownTypeIsRejected()
    {
        var token = await LoginAsync("admin@ebr.local");
        var company = await CreateCompanyAsync(token);

        using var response = await SendAsync(HttpMethod.Post, $"/api/companies/{company.Id}/representatives", new
        {
            fullName = "Representante Desconocido",
            documentNumber = "001-0000009-9",
            email = "desconocido@empresa.local",
            phoneNumber = "8095550099",
            representativeType = "SUPLENTE"
        }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<CompanyResponse> CreateCompanyAsync(string token)
    {
        var rnc = $"6{Random.Shared.Next(10000000, 99999999)}";
        using var response = await SendAsync(HttpMethod.Post, "/api/companies", new
        {
            legalName = "Empresa de Prueba de Perfil SRL",
            rnc,
            tradeName = "Perfil Prueba"
        }, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompanyResponse>())!;
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

    private async Task<T> PutJsonAsync<T>(string uri, object body, string token)
    {
        using var response = await SendAsync(HttpMethod.Put, uri, body, token);
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

    private sealed record CompanyResponse(
        int Id,
        string LegalName,
        string Rnc,
        string TradeName,
        string Address,
        string Municipality,
        string Province,
        string PhoneNumber,
        string Email,
        string EconomicActivity,
        bool IsActive,
        Guid VersionToken);

    private sealed record HistoryEntryResponse(int Id, int CompanyId, DateTimeOffset ChangedAt, Guid ChangedBy, string ChangesJson);
}
