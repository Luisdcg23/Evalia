using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class UserProfileEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public UserProfileEndpointTests(EbrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DelegateUserSeesRealAuthorizedCompany()
    {
        var token = await LoginAsync("delegado@ebr.local");
        var assignedCompanies = await GetAssignedCompanyIdsAsync(token);

        using var response = await GetAuthorizedAsync("/api/users/me", token);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        var company = Assert.Single(assignedCompanies);
        Assert.Equal([company], profile!.AuthorizedCompanyIds);
    }

    [Fact]
    public async Task CompanyAdministratorSeesRealAuthorizedCompany()
    {
        var token = await LoginAsync("empresa@ebr.local");
        var assignedCompanies = await GetAssignedCompanyIdsAsync(token);

        using var response = await GetAuthorizedAsync("/api/users/me", token);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        var company = Assert.Single(assignedCompanies);
        Assert.Equal([company], profile!.AuthorizedCompanyIds);
    }

    [Theory]
    [InlineData("admin@ebr.local")]
    [InlineData("coordinador@ebr.local")]
    [InlineData("tecnico@ebr.local")]
    public async Task RolesWithoutCompanyBindingHaveNoAuthorizedCompanies(string email)
    {
        var token = await LoginAsync(email);

        using var response = await GetAuthorizedAsync("/api/users/me", token);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        Assert.Empty(profile!.AuthorizedCompanyIds);
    }

    private async Task<List<int>> GetAssignedCompanyIdsAsync(string token)
    {
        using var response = await GetAuthorizedAsync("/api/companies", token);
        response.EnsureSuccessStatusCode();
        var companies = await response.Content.ReadFromJsonAsync<List<CompanyResponse>>();
        return companies!.Select(company => company.Id).ToList();
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

    private sealed record LoginResponse(string AccessToken);
    private sealed record CompanyResponse(int Id, string LegalName, string Rnc, string TradeName);
    private sealed record ProfileResponse(Guid Id, string Email, string FullName, string Role, List<int> AuthorizedCompanyIds);
}
