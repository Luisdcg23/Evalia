using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class RiskCatalogEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public RiskCatalogEndpointTests(EbrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FoodSubcategoryDerivesChemicalMicrobiologicalAndTotalPointsFromRiskLevels()
    {
        var token = await LoginAsync();
        var low = await CreateAsync<CreatedId>("/api/catalogs/risk-levels", new { name = "Bajo catálogo", points = 1 }, token);
        var high = await CreateAsync<CreatedId>("/api/catalogs/risk-levels", new { name = "Alto catálogo", points = 3 }, token);
        var category = await CreateAsync<CreatedId>("/api/catalogs/food-categories", new { name = "Lácteos" }, token);

        using var response = await PostAsync("/api/subcategories", new
        {
            categoryId = category.Id,
            name = "Quesos frescos",
            microbiologicalRiskLevelId = high.Id,
            chemicalRiskLevelId = low.Id
        }, token);
        var subcategory = await response.Content.ReadFromJsonAsync<SubcategoryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(subcategory);
        Assert.Equal(3m, subcategory.MicrobiologicalScore);
        Assert.Equal(1m, subcategory.ChemicalScore);
        Assert.Equal(3m, subcategory.TotalScore);
        Assert.Equal(high.Id, subcategory.TotalRiskLevelId);
    }

    [Fact]
    public async Task StructuralFactorPersistsOrderedWeightedOptions()
    {
        var token = await LoginAsync();

        using var response = await PostAsync("/api/catalogs/structural-factors", new
        {
            code = "HIST",
            name = "Historial sanitario",
            weight = 0.35m,
            order = 1,
            options = new[]
            {
                new { description = "Sin antecedentes", score = 1m, order = 1 },
                new { description = "Antecedentes graves", score = 3m, order = 2 }
            }
        }, token);
        var factor = await response.Content.ReadFromJsonAsync<FactorResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(factor);
        Assert.Equal(0.35m, factor.Weight);
        Assert.Equal(2, factor.Options.Count);
        Assert.Equal("Antecedentes graves", factor.Options[1].Description);
    }

    [Fact]
    public async Task FrequencyMatrixRejectsAnInvertedInterval()
    {
        var token = await LoginAsync();
        var level = await CreateAsync<CreatedId>("/api/catalogs/risk-levels", new { name = "Medio matriz", points = 2 }, token);

        using var response = await PostAsync("/api/catalogs/frequency-matrix", new
        {
            riskMin = 6.3m,
            minimumIncluded = true,
            riskMax = 3.6m,
            riskLevelId = level.Id,
            frequencyMonths = 6
        }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> LoginAsync()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@ebr.local",
            password = "EbrLocal2026!"
        });
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
    private sealed record SubcategoryResponse(
        decimal MicrobiologicalScore,
        decimal ChemicalScore,
        decimal TotalScore,
        int TotalRiskLevelId);
    private sealed record FactorOptionResponse(string Description, decimal Score, int Order);
    private sealed record FactorResponse(decimal Weight, IReadOnlyList<FactorOptionResponse> Options);
}
