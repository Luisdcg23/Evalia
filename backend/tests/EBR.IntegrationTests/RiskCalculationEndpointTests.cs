using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EBR.IntegrationTests;

/// <summary>
/// El cálculo de riesgo solo opera sobre una versión publicada de reglas, que es un artefacto
/// normativo: sus seis factores del establecimiento, sus escalas y sus bandas de frecuencia se cargan
/// con <see cref="RiskCatalogSeeder"/> desde la matriz oficial. Por eso la prueba parte del catálogo
/// sembrado en lugar de inventar factores sueltos por API.
/// </summary>
public sealed class RiskCalculationEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public RiskCalculationEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
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

        var catalog = await SeedPublishedRulesAsync();
        using var assignment = await PostAsync($"/api/companies/{company.Id}/subcategories", new
        {
            subcategoryIds = new[] { catalog.SubcategoryId }
        }, token);
        assignment.EnsureSuccessStatusCode();

        using var response = await PostAsync("/api/risk/calculate", new
        {
            companyId = company.Id,
            factorSelections = catalog.WorstSelections
        }, token);
        var result = await response.Content.ReadFromJsonAsync<RiskResponse>();

        // Riesgo del producto: Grasa láctea con riesgo microbiológico BAJO = 2 puntos.
        // Riesgo del establecimiento: peor opción de cada factor = 3, y los pesos suman 1, así que da 3.
        // Riesgo total 2 x 3 = 6, que cae en la banda (3.6, 6.3] -> inspección cada 6 meses.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(2m, result.ProductRisk);
        Assert.Equal(3m, result.EstablishmentRisk);
        Assert.Equal(6m, result.TotalRisk);
        Assert.Equal(catalog.MediumRiskLevelId, result.RiskLevelId);
        Assert.Equal(6, result.FrequencyMonths);
        Assert.Contains(catalog.WorstOptionDescription, result.FactorDetailsJson, StringComparison.Ordinal);

        using var historyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/companies/{company.Id}/risk");
        historyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var historyResponse = await _client.SendAsync(historyRequest);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<RiskResponse>>();
        Assert.Single(history!);
    }

    [Fact]
    public async Task CalculateIsRejectedWhenTheSelectionDoesNotCoverEveryFactor()
    {
        var token = await LoginAsync("admin@ebr.local");
        var company = await CreateAsync<CreatedId>("/api/companies", new
        {
            legalName = "Industria Incompleta SRL",
            rnc = $"6{Random.Shared.Next(10000000, 99999999)}",
            tradeName = "Incompleta"
        }, token);

        var catalog = await SeedPublishedRulesAsync();
        using var assignment = await PostAsync($"/api/companies/{company.Id}/subcategories", new
        {
            subcategoryIds = new[] { catalog.SubcategoryId }
        }, token);
        assignment.EnsureSuccessStatusCode();

        using var response = await PostAsync("/api/risk/calculate", new
        {
            companyId = company.Id,
            factorSelections = catalog.WorstSelections.Take(1).ToArray()
        }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<SeededCatalog> SeedPublishedRulesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        if (!await context.RiskRuleVersions.AnyAsync())
        {
            const string source = """
                ===== SHEET: Categorización_de_alimentos (1002x28) =====
                A1=CATEGORIA | B1=SUBCATEGORIA | C1=RIESGO MICROBIOLÓGICO | D1=PUNTAJE | E1=RIESGO QUÍMICO | F1=PUNTAJE | G1=RIESGO TOTAL
                A2=Productos lácteos | B2=Grasa láctea | C2=BAJO | D2=2 | G2=2
                """;
            await new RiskCatalogSeeder(context).SeedAsync(source, CancellationToken.None);
        }

        var version = await context.RiskRuleVersions
            .Where(value => value.IsPublished)
            .OrderByDescending(value => value.Version)
            .FirstAsync();
        var factors = await context.StructuralRiskFactors.AsNoTracking().Include(value => value.Options)
            .Where(value => value.RuleVersionId == version.Id && value.IsActive)
            .OrderBy(value => value.Order)
            .ToListAsync();
        var worst = factors
            .Select(factor => factor.Options.OrderByDescending(option => option.Score).First())
            .ToList();

        return new SeededCatalog(
            (await context.FoodSubcategories.SingleAsync(value => value.Name == "Grasa láctea")).Id,
            (await context.RiskLevels.SingleAsync(value => value.Name == "Riesgo medio")).Id,
            factors.Zip(worst, (factor, option) => new FactorSelection(factor.Id, option.Id)).ToArray(),
            worst[0].Description);
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

    private sealed record SeededCatalog(
        int SubcategoryId,
        int MediumRiskLevelId,
        IReadOnlyList<FactorSelection> WorstSelections,
        string WorstOptionDescription);

    private sealed record FactorSelection(int FactorId, int OptionId);
    private sealed record LoginResponse(string AccessToken);
    private sealed record CreatedId(int Id);
    private sealed record RiskResponse(
        decimal ProductRisk,
        decimal EstablishmentRisk,
        decimal TotalRisk,
        int RiskLevelId,
        int FrequencyMonths,
        string FactorDetailsJson);
}
