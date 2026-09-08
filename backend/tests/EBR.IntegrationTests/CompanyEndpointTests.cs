using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class CompanyEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public CompanyEndpointTests(EbrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdministratorCanCreateCompanyButDuplicateRncIsRejected()
    {
        var token = await LoginAsync("admin@ebr.local");
        var request = new { legalName = "Alimentos del Caribe SRL", rnc = "131999991", tradeName = "Caribe" };

        using var first = await PostAuthorizedAsync("/api/companies", request, token);
        using var duplicate = await PostAuthorizedAsync("/api/companies", request, token);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCannotCreateCompany()
    {
        var token = await LoginAsync("coordinador@ebr.local");

        using var response = await PostAuthorizedAsync("/api/companies", new
        {
            legalName = "Empresa no autorizada",
            rnc = "101010101",
            tradeName = "No autorizada"
        }, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompanyUserSeesOnlyAssignedCompanies()
    {
        var administratorToken = await LoginAsync("admin@ebr.local");
        using var create = await PostAuthorizedAsync("/api/companies", new
        {
            legalName = "Empresa aislada",
            rnc = $"9{Random.Shared.Next(10000000, 99999999)}",
            tradeName = "Aislada"
        }, administratorToken);
        create.EnsureSuccessStatusCode();

        var companyToken = await LoginAsync("empresa@ebr.local");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/companies");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", companyToken);
        using var response = await _client.SendAsync(request);
        var companies = await response.Content.ReadFromJsonAsync<List<CompanyResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var company = Assert.Single(companies!);
        Assert.Equal("130000001", company.Rnc);
    }

    [Fact]
    public async Task CompanyAdministratorCanCreateRepresentativeOnlyForAssignedCompany()
    {
        var companyToken = await LoginAsync("empresa@ebr.local");
        var assignedCompany = await GetOnlyAssignedCompanyAsync(companyToken);

        using var ownResponse = await PostAuthorizedAsync($"/api/companies/{assignedCompany.Id}/representatives", new
        {
            fullName = "Representante Autorizado",
            documentNumber = "001-1234567-8",
            email = "representante@example.local",
            phoneNumber = "8095550199"
        }, companyToken);

        var administratorToken = await LoginAsync("admin@ebr.local");
        var otherRnc = $"8{Random.Shared.Next(10000000, 99999999)}";
        using var createCompany = await PostAuthorizedAsync("/api/companies", new
        {
            legalName = "Empresa ajena",
            rnc = otherRnc,
            tradeName = "Ajena"
        }, administratorToken);
        var otherCompany = await createCompany.Content.ReadFromJsonAsync<CompanyResponse>();
        using var otherResponse = await PostAuthorizedAsync($"/api/companies/{otherCompany!.Id}/representatives", new
        {
            fullName = "Representante no autorizado",
            documentNumber = "001-7654321-0",
            email = "otro@example.local",
            phoneNumber = "8095550188"
        }, companyToken);

        Assert.Equal(HttpStatusCode.Created, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task CompanyWithRelatedDataCannotBeDeleted()
    {
        var administratorToken = await LoginAsync("admin@ebr.local");
        var rnc = $"7{Random.Shared.Next(10000000, 99999999)}";
        using var createCompany = await PostAuthorizedAsync("/api/companies", new
        {
            legalName = "Empresa con representante",
            rnc,
            tradeName = "Relacionada"
        }, administratorToken);
        var company = await createCompany.Content.ReadFromJsonAsync<CompanyResponse>();
        using var representative = await PostAuthorizedAsync($"/api/companies/{company!.Id}/representatives", new
        {
            fullName = "Representante",
            documentNumber = "001-1111111-1",
            email = "relacionado@example.local",
            phoneNumber = "8095550177"
        }, administratorToken);
        representative.EnsureSuccessStatusCode();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/companies/{company.Id}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administratorToken);
        using var deleteResponse = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task OnlyAdministratorCanModifyRiskCatalogs()
    {
        var administratorToken = await LoginAsync("admin@ebr.local");
        using var created = await PostAuthorizedAsync("/api/catalogs/risk-levels", new
        {
            name = "Muy alto",
            points = 4
        }, administratorToken);

        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        using var forbidden = await PostAuthorizedAsync("/api/catalogs/risk-levels", new
        {
            name = "No autorizado",
            points = 99
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
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

    private async Task<HttpResponseMessage> PostAuthorizedAsync(string uri, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<CompanyResponse> GetOnlyAssignedCompanyAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/companies");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var companies = await response.Content.ReadFromJsonAsync<List<CompanyResponse>>();
        return Assert.Single(companies!);
    }

    private sealed record LoginResponse(string AccessToken);

    private sealed record CompanyResponse(int Id, string LegalName, string Rnc, string TradeName);
}
