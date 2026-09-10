using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EBR.Domain.Evaluations;
using EBR.Domain.Identity;
using EBR.Infrastructure.Evaluations;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using EBR.Infrastructure.Risk;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EBR.IntegrationTests;

/// <summary>
/// Informe de evaluación versionado (fase 5.1, RF-16). Cada emisión del informe crea una versión
/// nueva y conserva las anteriores: el informe es el sustento del expediente y una corrección no
/// puede borrar lo que ya se comunicó.
/// </summary>
public sealed class EvaluationReportEndpointTests : IClassFixture<EbrApiFactory>
{
    private static int _scheduleOffsetDays;

    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public EvaluationReportEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AssignedTechnicianIssuesTheFirstVersionOfTheReport()
    {
        var scenario = await SubmitEvaluationAsync(2);

        var report = await PostJsonAsync<ReportResponse>($"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "El establecimiento cumple parcialmente con las condiciones evaluadas.",
            findings = "Se observó ausencia de registros de limpieza en el área de empaque.",
            recommendations = "Implementar bitácora diaria de limpieza y capacitar al personal."
        }, scenario.TechnicianToken);

        Assert.Equal(1, report.Version);
        Assert.Equal(scenario.InstanceId, report.EvaluationInstanceId);
        Assert.Equal("El establecimiento cumple parcialmente con las condiciones evaluadas.", report.ExecutiveSummary);
        Assert.Equal("Implementar bitácora diaria de limpieza y capacitar al personal.", report.Recommendations);
    }

    [Fact]
    public async Task EvaluationStillInCaptureDoesNotAdmitAReport()
    {
        var scenario = await StartEvaluationAsync(2);

        using var response = await SendAsync(HttpMethod.Post, $"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "Resumen prematuro.",
            findings = "Hallazgos prematuros.",
            recommendations = "Recomendaciones prematuras."
        }, scenario.TechnicianToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TechnicianWithoutTheAssignmentCannotIssueTheReport()
    {
        var scenario = await SubmitEvaluationAsync(2);
        var otherEmail = $"tecnico-informe-{Guid.NewGuid():N}@ebr.local";
        await CreateEvaluatorUserAsync(otherEmail);
        var otherToken = await LoginAsync(otherEmail);

        using var response = await SendAsync(HttpMethod.Post, $"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "Resumen de un técnico ajeno.",
            findings = "Hallazgos de un técnico ajeno.",
            recommendations = "Recomendaciones de un técnico ajeno."
        }, otherToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IssuingTheReportAgainAddsAVersionAndKeepsThePreviousOne()
    {
        var scenario = await SubmitEvaluationAsync(2);

        await PostJsonAsync<ReportResponse>($"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "Resumen inicial.",
            findings = "Hallazgos iniciales.",
            recommendations = "Recomendaciones iniciales."
        }, scenario.TechnicianToken);

        var corrected = await PostJsonAsync<ReportResponse>($"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "Resumen corregido.",
            findings = "Hallazgos corregidos.",
            recommendations = "Recomendaciones corregidas."
        }, scenario.TechnicianToken);

        Assert.Equal(2, corrected.Version);

        var versions = await GetJsonAsync<List<ReportResponse>>(
            $"/api/evaluations/{scenario.InstanceId}/report/versions", scenario.TechnicianToken);

        Assert.Equal([1, 2], versions.Select(version => version.Version));
        Assert.Equal("Resumen inicial.", versions[0].ExecutiveSummary);
        Assert.Equal("Resumen corregido.", versions[1].ExecutiveSummary);
    }

    [Fact]
    public async Task CoordinatorConsultsTheCurrentVersionOfTheReport()
    {
        var scenario = await SubmitEvaluationAsync(2);

        await PostJsonAsync<ReportResponse>($"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "Resumen inicial.",
            findings = "Hallazgos iniciales.",
            recommendations = "Recomendaciones iniciales."
        }, scenario.TechnicianToken);

        await PostJsonAsync<ReportResponse>($"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "Resumen corregido.",
            findings = "Hallazgos corregidos.",
            recommendations = "Recomendaciones corregidas."
        }, scenario.TechnicianToken);

        var current = await GetJsonAsync<ReportResponse>(
            $"/api/evaluations/{scenario.InstanceId}/report", scenario.CoordinatorToken);

        Assert.Equal(2, current.Version);
        Assert.Equal("Resumen corregido.", current.ExecutiveSummary);
    }

    [Fact]
    public async Task TechnicianWithoutTheAssignmentCannotReadTheReport()
    {
        var scenario = await SubmitEvaluationAsync(2);
        await PostJsonAsync<ReportResponse>($"/api/evaluations/{scenario.InstanceId}/report", new
        {
            executiveSummary = "Resumen inicial.",
            findings = "Hallazgos iniciales.",
            recommendations = "Recomendaciones iniciales."
        }, scenario.TechnicianToken);

        var otherEmail = $"tecnico-lectura-{Guid.NewGuid():N}@ebr.local";
        await CreateEvaluatorUserAsync(otherEmail);
        var otherToken = await LoginAsync(otherEmail);

        using var versions = await SendAsync(HttpMethod.Get, $"/api/evaluations/{scenario.InstanceId}/report/versions", null, otherToken);
        Assert.Equal(HttpStatusCode.Forbidden, versions.StatusCode);
    }

    private async Task<EvaluationScenario> SubmitEvaluationAsync(int questionCount)
    {
        var scenario = await StartEvaluationAsync(questionCount);

        foreach (var itemId in scenario.ItemIds)
        {
            await PutJsonAsync<ResponseResponse>(
                $"/api/evaluations/{scenario.InstanceId}/responses/{itemId}",
                new { optionCode = "CP" },
                scenario.TechnicianToken);
        }

        await PostJsonAsync<InstanceResponse>($"/api/evaluations/{scenario.InstanceId}/submit", null, scenario.TechnicianToken);

        return scenario;
    }

    private async Task<EvaluationScenario> StartEvaluationAsync(int questionCount)
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(questionCount);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        return new EvaluationScenario(caseId, instance.Id, technicianToken, coordinatorToken, itemIds);
    }

    private async Task<int> CreateScheduledCaseAsync(string coordinatorToken, Guid technicianId)
    {
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", coordinatorToken));
        var created = await PostJsonAsync<CaseResponse>("/api/cases/institutional", new
        {
            companyId = company.Id,
            reason = $"Caso de prueba de informe {Guid.NewGuid():N}"
        }, coordinatorToken);

        await PostJsonAsync<object>($"/api/cases/{created.Id}/assign", new
        {
            technicianId,
            reason = "Asignación previa al informe"
        }, coordinatorToken);

        var offsetDays = Interlocked.Increment(ref _scheduleOffsetDays);
        await PostJsonAsync<object>($"/api/cases/{created.Id}/schedule", new
        {
            scheduledFor = DateTimeOffset.UtcNow.Date.AddDays(120 + offsetDays).AddHours(9),
            reason = "Programación previa al informe"
        }, coordinatorToken);

        return created.Id;
    }

    private async Task<List<int>> CreatePublishedTemplateAsync(int questionCount)
    {
        await EnsureRiskCatalogAsync();
        var adminToken = await LoginAsync("admin@ebr.local");
        var template = await PostJsonAsync<TemplateResponse>("/api/evaluation-templates", new
        {
            name = $"Plantilla de informe {Guid.NewGuid():N}"
        }, adminToken);

        var itemIds = new List<int>();
        for (var index = 0; index < questionCount; index++)
        {
            var item = await PostJsonAsync<TemplateItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
            {
                code = $"Q{index}-{Guid.NewGuid():N}",
                description = $"Pregunta de prueba {index}",
                itemType = "QUESTION",
                order = index,
                weight = 1,
                isRequired = true,
                allowsNotApplicable = true,
                responseType = "BPM_OPTION"
            }, adminToken);
            itemIds.Add(item.Id);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            foreach (var option in BpmResponseOptions.All)
            {
                context.EvaluationResponseOptions.Add(new EvaluationResponseOption
                {
                    TemplateId = template.Id,
                    Code = option.Code,
                    Name = option.Name,
                    Value = option.Value,
                    CountsTowardDenominator = option.CountsTowardDenominator
                });
            }

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

        await PostJsonAsync<TemplateResponse>($"/api/evaluation-templates/{template.Id}/publish", null, adminToken);

        return itemIds;
    }

    private async Task EnsureRiskCatalogAsync()
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

        var company = await context.Companies.SingleAsync(value => value.Rnc == "130000001");
        var subcategory = await context.FoodSubcategories.SingleAsync(value => value.Name == "Grasa láctea");
        if (!await context.CompanyFoodSubcategories.AnyAsync(value => value.CompanyId == company.Id && value.SubcategoryId == subcategory.Id))
            context.CompanyFoodSubcategories.Add(new() { CompanyId = company.Id, SubcategoryId = subcategory.Id });

        var version = await context.RiskRuleVersions.OrderByDescending(value => value.Version).FirstAsync();
        var factors = await context.StructuralRiskFactors.Include(value => value.Options)
            .Where(value => value.RuleVersionId == version.Id).ToListAsync();
        foreach (var factor in factors)
        {
            if (await context.CompanyRiskFactorValues.AnyAsync(value => value.CompanyId == company.Id && value.FactorId == factor.Id && value.IsCurrent))
                continue;
            context.CompanyRiskFactorValues.Add(new()
            {
                CompanyId = company.Id,
                FactorId = factor.Id,
                OptionId = factor.Options.OrderBy(value => value.Score).First().Id,
                RegisteredAt = DateTimeOffset.UtcNow,
                IsCurrent = true,
                RegisteredBy = await GetUserIdAsync("coordinador@ebr.local")
            });
        }
        await context.SaveChangesAsync();
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        var token = await LoginAsync(email);
        var me = await GetJsonAsync<MeResponse>("/api/users/me", token);
        return me.Id;
    }

    private async Task<Guid> CreateEvaluatorUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Técnico adicional de informe",
            ApprovalStatus = UserApprovalStatus.Approved
        };
        var createResult = await userManager.CreateAsync(user, "EbrLocal2026!");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));
        var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.Evaluator);
        Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        return user.Id;
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

    private async Task<T> PostJsonAsync<T>(string uri, object? body, string token)
    {
        using var response = await SendAsync(HttpMethod.Post, uri, body, token);
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

    private sealed record EvaluationScenario(
        int CaseId, int InstanceId, string TechnicianToken, string CoordinatorToken, List<int> ItemIds);

    private sealed record LoginResponse(string AccessToken);
    private sealed record CompanyResponse(int Id);
    private sealed record CaseResponse(int Id, string Status);
    private sealed record MeResponse(Guid Id, string Email, string FullName, string Role);
    private sealed record TemplateResponse(int Id, string Name, int Version, string Status);
    private sealed record TemplateItemResponse(int Id, int TemplateId, string Code);
    private sealed record InstanceResponse(int Id, int CaseId, int TemplateId, string Status);
    private sealed record ResponseResponse(int Id, int EvaluationInstanceId, int TemplateItemId, string OptionCode);

    private sealed record ReportResponse(
        int Id, int EvaluationInstanceId, int Version, string Status,
        string ExecutiveSummary, string Findings, string Recommendations,
        DateTimeOffset CreatedAt, Guid CreatedBy);
}
