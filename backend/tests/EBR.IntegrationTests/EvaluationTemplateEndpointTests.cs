using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EBR.Domain.Evaluations;
using EBR.Infrastructure.Evaluations;
using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EBR.IntegrationTests;

public sealed class EvaluationTemplateEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public EvaluationTemplateEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DraftSupportsADeepEditableHierarchyInOneItemCollection()
    {
        var token = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = "Inspección BPM" }, token);
        var chapter = await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "1",
            description = "Establecimiento",
            itemType = "CHAPTER",
            order = 1
        }, token);
        var section = await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            parentId = chapter.Id,
            code = "1.1",
            description = "Ubicación y estructura",
            itemType = "SECTION",
            order = 1
        }, token);
        var question = await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            parentId = section.Id,
            code = "1.1.1",
            description = "La ubicación es adecuada",
            itemType = "QUESTION",
            order = 1,
            weight = 2.5m,
            isRequired = true,
            isCritical = true,
            allowsNotApplicable = false,
            responseType = "COMPLIANCE"
        }, token);

        using var listResponse = await GetAsync($"/api/evaluation-templates/{template.Id}/items", token);
        var items = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(3, items!.Count);
        Assert.Equal(section.Id, question.ParentId);
        Assert.Equal(2.5m, question.Weight);
        Assert.True(question.IsCritical);
    }

    [Fact]
    public async Task PublishedTemplateIsImmutableAndNewVersionClonesItsHierarchy()
    {
        // Otras pruebas de esta misma clase (comparten base de datos vía el fixture) pueden haber
        // sembrado ya la matriz de riesgo, cuyo factor BPM exige que el número de bandas de
        // calificación de la plantilla coincida exactamente para poder publicarla (ver
        // `EvaluationTemplateEndpoints.ValidateQualificationBandsAsync`). Esta prueba no ejercita esa
        // validación (tiene su propio caso dedicado); solo necesita que el publish no se rechace.
        await EnsureRiskCatalogAsync();
        var token = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = "Plantilla versionada" }, token);
        await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "1",
            description = "Higiene",
            itemType = "CHAPTER",
            order = 1
        }, token);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var order = 0;
            foreach (var band in BpmTemplateData.QualificationBands)
            {
                context.EvaluationQualificationRules.Add(new EvaluationQualificationRule
                {
                    TemplateId = template.Id,
                    Code = band.Code,
                    Description = band.Description,
                    Classification = band.Classification,
                    Action = band.Action,
                    MinPercentage = band.MinPercentage,
                    MinIncluded = band.MinIncluded,
                    MaxPercentage = band.MaxPercentage,
                    MaxIncluded = band.MaxIncluded,
                    Order = order++
                });
            }
            await context.SaveChangesAsync();
        }
        await PostAsync<TemplateResponse>($"/api/evaluation-templates/{template.Id}/publish", new { }, token);

        using var locked = await SendAsync(HttpMethod.Post, $"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "2",
            description = "No permitido",
            itemType = "CHAPTER",
            order = 2
        }, token);
        var versionTwo = await PostAsync<TemplateResponse>($"/api/evaluation-templates/{template.Id}/versions", new { }, token);
        using var clonedResponse = await GetAsync($"/api/evaluation-templates/{versionTwo.Id}/items", token);
        var cloned = await clonedResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();

        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal(2, versionTwo.Version);
        Assert.Equal("DRAFT", versionTwo.Status);
        Assert.Single(cloned!);
        Assert.Equal("Higiene", cloned![0].Description);
    }

    [Fact]
    public async Task TechnicianCanReadItemsButCannotWriteThem()
    {
        var adminToken = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = "Lectura del técnico" }, adminToken);
        await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "1",
            description = "Higiene del personal",
            itemType = "CHAPTER",
            order = 1
        }, adminToken);

        var technicianToken = await LoginAsync("tecnico@ebr.local");
        using var listTemplates = await GetAsync("/api/evaluation-templates", technicianToken);
        using var listItems = await GetAsync($"/api/evaluation-templates/{template.Id}/items", technicianToken);
        using var forbiddenCreate = await SendAsync(HttpMethod.Post, $"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "2",
            description = "Intento no permitido",
            itemType = "CHAPTER",
            order = 2
        }, technicianToken);

        Assert.Equal(HttpStatusCode.OK, listTemplates.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listItems.StatusCode);
        var items = await listItems.Content.ReadFromJsonAsync<List<ItemResponse>>();
        Assert.Single(items!);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
    }

    [Fact]
    public async Task CreatingATemplateSeedsTheFourStandardBpmResponseOptions()
    {
        var token = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = $"Plantilla de opciones {Guid.NewGuid():N}" }, token);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        var options = await context.EvaluationResponseOptions.AsNoTracking()
            .Where(option => option.TemplateId == template.Id)
            .OrderBy(option => option.Order)
            .ToListAsync();

        Assert.Equal(4, options.Count);
        Assert.Equal(["C", "CP", "IT", "NA"], options.Select(option => option.Code));
        Assert.Equal(1m, options[0].Value);
        Assert.True(options[0].CountsTowardDenominator);
        Assert.Null(options[3].Value);
        Assert.False(options[3].CountsTowardDenominator);
    }

    [Fact]
    public async Task PublishRejectsATemplateWhoseQualificationBandCountDoesNotMatchTheVigentBpmFactor()
    {
        await EnsureRiskCatalogAsync();
        var token = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = $"Plantilla de bandas incompletas {Guid.NewGuid():N}" }, token);
        await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "1", description = "Capítulo único", itemType = "CHAPTER", order = 1
        }, token);

        // El factor BPM vigente que siembra EnsureRiskCatalogAsync tiene 4 opciones; esta plantilla
        // solo declara una banda, así que debe rechazarse antes de publicar.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            context.EvaluationQualificationRules.Add(new EvaluationQualificationRule
            {
                TemplateId = template.Id,
                Code = "CAL-1",
                Description = "Única banda de prueba",
                Classification = "Prueba",
                Action = "Prueba",
                MinPercentage = 0,
                MinIncluded = true,
                MaxPercentage = 100,
                MaxIncluded = true,
                Order = 1
            });
            await context.SaveChangesAsync();
        }

        using var response = await SendAsync(HttpMethod.Post, $"/api/evaluation-templates/{template.Id}/publish", new { }, token);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("banda", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("4", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivateRejectsATemplateThatIsNotPublished()
    {
        var token = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = $"Plantilla sin publicar {Guid.NewGuid():N}" }, token);

        using var response = await SendAsync(HttpMethod.Post, $"/api/evaluation-templates/{template.Id}/activate", new { }, token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ActivatingAPublishedTemplateThatMeetsTheBandContractUpdatesTheActiveTemplate()
    {
        await EnsureRiskCatalogAsync();
        var token = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = $"Plantilla activable {Guid.NewGuid():N}" }, token);
        await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "1", description = "Capítulo único", itemType = "CHAPTER", order = 1
        }, token);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var order = 0;
            foreach (var band in BpmTemplateData.QualificationBands)
            {
                context.EvaluationQualificationRules.Add(new EvaluationQualificationRule
                {
                    TemplateId = template.Id,
                    Code = band.Code,
                    Description = band.Description,
                    Classification = band.Classification,
                    Action = band.Action,
                    MinPercentage = band.MinPercentage,
                    MinIncluded = band.MinIncluded,
                    MaxPercentage = band.MaxPercentage,
                    MaxIncluded = band.MaxIncluded,
                    Order = order++
                });
            }
            await context.SaveChangesAsync();
        }
        await PostAsync<TemplateResponse>($"/api/evaluation-templates/{template.Id}/publish", new { }, token);

        var activated = await PostAsync<ActiveTemplateResponse>($"/api/evaluation-templates/{template.Id}/activate", new { }, token);
        using var activeResponse = await GetAsync("/api/evaluation-templates/active", token);
        var active = await activeResponse.Content.ReadFromJsonAsync<ActiveTemplateResponse>();

        Assert.Equal(template.Id, activated.TemplateId);
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.Equal(template.Id, active!.TemplateId);
    }

    private async Task EnsureRiskCatalogAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        if (await context.RiskRuleVersions.AnyAsync()) return;

        const string source = """
            ===== SHEET: Categorización_de_alimentos (1002x28) =====
            A1=CATEGORIA | B1=SUBCATEGORIA | C1=RIESGO MICROBIOLÓGICO | D1=PUNTAJE | E1=RIESGO QUÍMICO | F1=PUNTAJE | G1=RIESGO TOTAL
            A2=Productos lácteos | B2=Grasa láctea | C2=BAJO | D2=2 | G2=2
            """;
        await new RiskCatalogSeeder(context).SeedAsync(source, CancellationToken.None);
    }

    private async Task<string> LoginAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "EbrLocal2026!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<T> PostAsync<T>(string uri, object body, string token)
    {
        using var response = await SendAsync(HttpMethod.Post, uri, body, token);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> GetAsync(string uri, string token) =>
        await SendAsync(HttpMethod.Get, uri, null, token);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record TemplateResponse(int Id, int Version, string Status);
    private sealed record ActiveTemplateResponse(int? TemplateId, DateTimeOffset? ActivatedAt);
    private sealed record ItemResponse(
        int Id,
        int? ParentId,
        string Code,
        string Description,
        decimal? Weight,
        bool IsCritical);
}
