using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
/// Evidencias de la evaluación en campo (fase 4.1): el binario vive en el almacenamiento de objetos
/// y solo los metadatos en PostgreSQL. Las pruebas corren contra la implementación en memoria de
/// <c>IEvidenceStorage</c> que instala <see cref="EbrApiFactory"/>, así que no necesitan un MinIO real.
/// </summary>
public sealed class EvaluationEvidenceEndpointTests : IClassFixture<EbrApiFactory>
{
    private static int _scheduleOffsetDays;

    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public EvaluationEvidenceEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AssignedTechnicianAttachesEvidenceToAResponse()
    {
        var scenario = await StartEvaluationAsync(2);
        var content = Encoding.UTF8.GetBytes("contenido binario de la fotografía de campo");
        var response = await SaveResponseAsync(scenario, scenario.ItemIds[0], "IT");

        var evidence = await UploadAsync(
            scenario, content, "evidencia-camara.jpg", "image/jpeg", scenario.ItemIds[0]);

        Assert.Equal("evidencia-camara.jpg", evidence.FileName);
        Assert.Equal("image/jpeg", evidence.MimeType);
        Assert.Equal(content.Length, evidence.SizeBytes);
        Assert.Equal(Sha256Of(content), evidence.Hash);
        Assert.False(string.IsNullOrWhiteSpace(evidence.StorageKey));
        Assert.Equal(response.Id, evidence.EvaluationResponseId);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        var stored = await context.EvaluationEvidences.SingleAsync(value => value.Id == evidence.Id);
        Assert.Equal(scenario.InstanceId, stored.EvaluationInstanceId);
        Assert.Equal(response.Id, stored.EvaluationResponseId);
        Assert.Equal(evidence.StorageKey, stored.StorageKey);
    }

    [Fact]
    public async Task EvidenceIsDownloadedWithTheSameBytesItWasUploaded()
    {
        var scenario = await StartEvaluationAsync(1);
        var content = Encoding.UTF8.GetBytes("acta firmada en formato PDF");
        await SaveResponseAsync(scenario, scenario.ItemIds[0], "C");
        var evidence = await UploadAsync(scenario, content, "acta.pdf", "application/pdf", scenario.ItemIds[0]);

        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/evaluations/{scenario.InstanceId}/evidence/{evidence.Id}/content",
            null,
            scenario.TechnicianToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task TechnicianWithoutTheAssignmentCannotAttachEvidence()
    {
        var scenario = await StartEvaluationAsync(1);
        var otherEmail = $"tecnico-evidencia-{Guid.NewGuid():N}@ebr.local";
        await CreateEvaluatorUserAsync(otherEmail);
        var otherToken = await LoginAsync(otherEmail);

        using var response = await SendUploadAsync(
            scenario, Encoding.UTF8.GetBytes("intento ajeno"), "ajena.jpg", "image/jpeg", null, otherToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        Assert.Empty(await context.EvaluationEvidences.Where(value => value.EvaluationInstanceId == scenario.InstanceId).ToListAsync());
    }

    [Fact]
    public async Task TechnicianWithoutTheAssignmentCannotDownloadEvidence()
    {
        var scenario = await StartEvaluationAsync(1);
        var evidence = await UploadAsync(scenario, Encoding.UTF8.GetBytes("foto de la cámara de frío"), "camara.png", "image/png", null);
        var otherEmail = $"tecnico-descarga-{Guid.NewGuid():N}@ebr.local";
        await CreateEvaluatorUserAsync(otherEmail);
        var otherToken = await LoginAsync(otherEmail);

        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/evaluations/{scenario.InstanceId}/evidence/{evidence.Id}/content",
            null,
            otherToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EvidenceOfAnUnsupportedTypeIsRejected()
    {
        var scenario = await StartEvaluationAsync(1);

        using var response = await SendUploadAsync(
            scenario, Encoding.UTF8.GetBytes("<html></html>"), "reporte.html", "text/html", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        Assert.Empty(await context.EvaluationEvidences.Where(value => value.EvaluationInstanceId == scenario.InstanceId).ToListAsync());
    }

    [Fact]
    public async Task EvidenceLargerThanTheAllowedSizeIsRejected()
    {
        var scenario = await StartEvaluationAsync(1);
        var content = new byte[EvidencePolicy.MaxSizeBytes + 1];

        using var response = await SendUploadAsync(scenario, content, "enorme.jpg", "image/jpeg", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        Assert.Empty(await context.EvaluationEvidences.Where(value => value.EvaluationInstanceId == scenario.InstanceId).ToListAsync());
    }

    [Fact]
    public async Task EvidenceForAQuestionWithoutASavedResponseIsRejected()
    {
        var scenario = await StartEvaluationAsync(2);

        using var response = await SendUploadAsync(
            scenario, Encoding.UTF8.GetBytes("foto sin respuesta"), "foto.jpg", "image/jpeg", scenario.ItemIds[1]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmittedEvaluationDoesNotAdmitNewEvidence()
    {
        var scenario = await StartEvaluationAsync(1);
        await SaveResponseAsync(scenario, scenario.ItemIds[0], "C");
        await PostJsonAsync<InstanceResponse>($"/api/evaluations/{scenario.InstanceId}/submit", null, scenario.TechnicianToken);

        using var response = await SendUploadAsync(
            scenario, Encoding.UTF8.GetBytes("evidencia tardía"), "tardia.jpg", "image/jpeg", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CoordinatorListsAndDownloadsTheEvidenceOfTheEvaluation()
    {
        var scenario = await StartEvaluationAsync(1);
        var content = Encoding.UTF8.GetBytes("fotografía del área de empaque");
        var evidence = await UploadAsync(scenario, content, "empaque.jpg", "image/jpeg", null);

        var listed = await GetJsonAsync<List<EvidenceResponse>>(
            $"/api/evaluations/{scenario.InstanceId}/evidence", scenario.CoordinatorToken);

        var single = Assert.Single(listed);
        Assert.Equal(evidence.Id, single.Id);
        Assert.Equal("empaque.jpg", single.FileName);

        using var download = await SendAsync(
            HttpMethod.Get,
            $"/api/evaluations/{scenario.InstanceId}/evidence/{evidence.Id}/content",
            null,
            scenario.CoordinatorToken);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task CompanyUserCannotReadTheEvidenceOfAnEvaluation()
    {
        var scenario = await StartEvaluationAsync(1);
        await UploadAsync(scenario, Encoding.UTF8.GetBytes("evidencia reservada"), "reservada.jpg", "image/jpeg", null);
        var companyToken = await LoginAsync("empresa@ebr.local");

        using var response = await SendAsync(
            HttpMethod.Get, $"/api/evaluations/{scenario.InstanceId}/evidence", null, companyToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string Sha256Of(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private async Task<EvidenceResponse> UploadAsync(
        EvaluationScenario scenario,
        byte[] content,
        string fileName,
        string mimeType,
        int? templateItemId)
    {
        using var response = await SendUploadAsync(scenario, content, fileName, mimeType, templateItemId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EvidenceResponse>())!;
    }

    private async Task<HttpResponseMessage> SendUploadAsync(
        EvaluationScenario scenario,
        byte[] content,
        string fileName,
        string mimeType,
        int? templateItemId,
        string? token = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(file, "file", fileName);
        if (templateItemId is not null)
            form.Add(new StringContent(templateItemId.Value.ToString(CultureInfo.InvariantCulture)), "templateItemId");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/evaluations/{scenario.InstanceId}/evidence")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? scenario.TechnicianToken);
        return await _client.SendAsync(request);
    }

    private async Task<ResponseResponse> SaveResponseAsync(EvaluationScenario scenario, int itemId, string optionCode) =>
        await PutJsonAsync<ResponseResponse>(
            $"/api/evaluations/{scenario.InstanceId}/responses/{itemId}",
            new { optionCode },
            scenario.TechnicianToken);

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
        var offsetDays = Interlocked.Increment(ref _scheduleOffsetDays);
        var created = await PostJsonAsync<CaseResponse>("/api/cases/institutional", new
        {
            companyId = await CompanyIdAsync(coordinatorToken),
            reason = "Evaluación institucional con evidencias",
            observations = ""
        }, coordinatorToken);

        await PostJsonAsync<object>($"/api/cases/{created.Id}/assign", new
        {
            technicianId,
            reason = "Asignación previa a la evaluación"
        }, coordinatorToken);

        await PostJsonAsync<object>($"/api/cases/{created.Id}/schedule", new
        {
            scheduledFor = DateTimeOffset.UtcNow.Date.AddDays(60 + offsetDays).AddHours(9),
            reason = "Programación previa a la evaluación"
        }, coordinatorToken);

        return created.Id;
    }

    private async Task<int> CompanyIdAsync(string coordinatorToken)
    {
        var companies = await GetJsonAsync<List<CompanyResponse>>("/api/companies", coordinatorToken);
        return companies[0].Id;
    }

    private async Task<List<int>> CreatePublishedTemplateAsync(int questionCount)
    {
        await EnsureRiskCatalogAsync();
        var adminToken = await LoginAsync("admin@ebr.local");
        var template = await PostJsonAsync<TemplateResponse>("/api/evaluation-templates", new
        {
            name = $"Plantilla de evidencias {Guid.NewGuid():N}"
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
        await context.SaveChangesAsync();

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
            FullName = "Técnico adicional de evidencias",
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

    private sealed record EvidenceResponse(
        int Id, int EvaluationInstanceId, int? EvaluationResponseId, int? TemplateItemId,
        string FileName, string MimeType, long SizeBytes, string Hash, string StorageKey,
        DateTimeOffset UploadedAt, Guid UploadedBy);
}
