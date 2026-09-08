using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class RiskCalculationEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public RiskCalculationEndpointTests(EbrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CalculatePersistsReproducibleRiskAndReturnsConfiguredFrequency()
    {
        var token = await LoginAsync("admin@ebr.local");
        var company = await CreateAsync<CreatedId>("/api/companies", new
        {
            legalName = "Industria de Riesgo SRL",
            rnc = $"6{Random.Shared.Next(10000000, 99999999)}",
            tradeName = "Riesgo"
        }, token);
        var low = await CreateAsync<CreatedId>("/api/catalogs/risk-levels", new { name = "Bajo cálculo", points = 1 }, token);
        var medium = await CreateAsync<CreatedId>("/api/catalogs/risk-levels", new { name = "Medio cálculo", points = 2 }, token);
        var high = await CreateAsync<CreatedId>("/api/catalogs/risk-levels", new { name = "Alto cálculo", points = 3 }, token);
        var category = await CreateAsync<CreatedId>("/api/catalogs/food-categories", new { name = "Cárnicos cálculo" }, token);
        var subcategory = await CreateAsync<CreatedId>("/api/subcategories", new
        {
            categoryId = category.Id,
            name = "Embutidos cálculo",
            microbiologicalRiskLevelId = high.Id,
            chemicalRiskLevelId = low.Id
        }, token);
        using var assignment = await PostAsync($"/api/companies/{company.Id}/subcategories", new
        {
            subcategoryIds = new[] { subcategory.Id }
        }, token);
        assignment.EnsureSuccessStatusCode();
        var factor = await CreateAsync<FactorResponse>("/api/catalogs/structural-factors", new
        {
            code = $"COND-{Guid.NewGuid():N}",
            name = "Condición sanitaria cálculo",
            weight = 1m,
            order = 1,
            options = new[] { new { description = "Condición media", score = 2m, order = 1 } }
        }, token);
        await CreateAsync<CreatedId>("/api/catalogs/frequency-matrix", new { riskMin = 0m, minimumIncluded = true, riskMax = 3.6m, riskLevelId = low.Id, frequencyMonths = 12 }, token);
        await CreateAsync<CreatedId>("/api/catalogs/frequency-matrix", new { riskMin = 3.6m, minimumIncluded = false, riskMax = 6.3m, riskLevelId = medium.Id, frequencyMonths = 6 }, token);
        await CreateAsync<CreatedId>("/api/catalogs/frequency-matrix", new { riskMin = 6.3m, minimumIncluded = false, riskMax = 9m, riskLevelId = high.Id, frequencyMonths = 3 }, token);

        using var response = await PostAsync("/api/risk/calculate", new
        {
            companyId = company.Id,
            factorSelections = new[] { new { factorId = factor.Id, optionId = factor.Options[0].Id } }
        }, token);
        var result = await response.Content.ReadFromJsonAsync<RiskResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(3m, result.ProductRisk);
        Assert.Equal(2m, result.EstablishmentRisk);
        Assert.Equal(6m, result.TotalRisk);
        Assert.Equal(medium.Id, result.RiskLevelId);
        Assert.Equal(6, result.FrequencyMonths);
        Assert.Contains("Condición media", result.FactorDetailsJson, StringComparison.Ordinal);

        using var historyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/companies/{company.Id}/risk");
        historyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var historyResponse = await _client.SendAsync(historyRequest);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<RiskResponse>>();
        Assert.Single(history!);
    }

    private async Task<string> LoginAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "EbrLocal2026!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<T> CreateAsync<T>(string uri, object body, string token)
    {
        using var response = await PostAsync(uri, body, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> PostAsync(string uri, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record CreatedId(int Id);
    private sealed record FactorOptionResponse(int Id);
    private sealed record FactorResponse(int Id, IReadOnlyList<FactorOptionResponse> Options);
    private sealed record RiskResponse(
        decimal ProductRisk,
        decimal EstablishmentRisk,
        decimal TotalRisk,
        int RiskLevelId,
        int FrequencyMonths,
        string FactorDetailsJson);
}
