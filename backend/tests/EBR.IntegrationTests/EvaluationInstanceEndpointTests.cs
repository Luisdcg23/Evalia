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

public sealed class EvaluationInstanceEndpointTests : IClassFixture<EbrApiFactory>
{
    // Todos los métodos de esta clase comparten la base del EbrApiFactory (mismo técnico
    // seed "tecnico@ebr.local"), así que cada caso programado necesita una fecha propia para no
    // solaparse con la ventana de 2 horas de otra prueba (CaseScheduleWindow.HoursPerEvaluation).
    private static int _scheduleOffsetDays;

    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public EvaluationInstanceEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AssignedTechnicianStartsEvaluationFromScheduledCase()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        await CreatePublishedTemplateAsync(3);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);

        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        Assert.Equal(caseId, instance.CaseId);
        Assert.Equal("IN_PROGRESS", instance.Status);
        Assert.Null(instance.SubmittedAt);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var stored = await context.EvaluationInstances.SingleAsync(value => value.Id == instance.Id);
            Assert.True(stored.RiskRuleVersionId > 0);
            Assert.NotEqual(Guid.Empty, stored.TemplateFamilyId);
        }

        var updatedCase = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("IN_EVALUATION", updatedCase.Status);
    }

    [Fact]
    public async Task StartUsesTheExplicitlyActivatedTemplateNotTheMostRecentlyPublished()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var adminToken = await LoginAsync("admin@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");

        var (activatedTemplateId, _) = await CreatePublishedTemplateWithIdAsync(1);

        // Se publica una plantilla más nueva DESPUÉS de activar la primera y deliberadamente no se
        // activa: si StartAsync todavía eligiera "la publicada más recientemente" (el criterio anterior
        // a este arreglo), tomaría esta por error.
        var (laterPublishedTemplateId, _) = await CreateAndPublishTemplateAsync(1);
        Assert.True(laterPublishedTemplateId > activatedTemplateId);

        var active = await GetJsonAsync<ActiveTemplateResponse>("/api/evaluation-templates/active", adminToken);
        Assert.Equal(activatedTemplateId, active.TemplateId);

        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        Assert.Equal(activatedTemplateId, instance.TemplateId);
    }

    [Fact]
    public async Task DifferentTechnicianCannotStartEvaluation()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        await CreatePublishedTemplateAsync(3);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);

        var otherTechnicianEmail = $"tecnico-otro-{Guid.NewGuid():N}@ebr.local";
        await CreateEvaluatorUserAsync(otherTechnicianEmail);
        var otherToken = await LoginAsync(otherTechnicianEmail);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/evaluations", null, otherToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var stillScheduled = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("SCHEDULED", stillScheduled.Status);
    }

    [Fact]
    public async Task DifferentTechnicianCannotReadEvaluationProgress()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        await CreatePublishedTemplateAsync(1);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);
        var otherEmail = $"tecnico-lector-{Guid.NewGuid():N}@ebr.local";
        await CreateEvaluatorUserAsync(otherEmail);
        var otherToken = await LoginAsync(otherEmail);

        using var response = await SendAsync(HttpMethod.Get, $"/api/evaluations/{instance.Id}", null, otherToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SavingResponseTwiceUpdatesInsteadOfDuplicating()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(3);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        var first = await PutJsonAsync<ResponseResponse>(
            $"/api/evaluations/{instance.Id}/responses/{itemIds[0]}",
            new { optionCode = "C", observations = "Todo en orden" },
            technicianToken);
        Assert.Equal("C", first.OptionCode);

        var second = await PutJsonAsync<ResponseResponse>(
            $"/api/evaluations/{instance.Id}/responses/{itemIds[0]}",
            new { optionCode = "CP", observations = "Corrección menor" },
            technicianToken);
        Assert.Equal("CP", second.OptionCode);
        Assert.Equal(first.Id, second.Id);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        var rows = await context.EvaluationResponses
            .Where(response => response.EvaluationInstanceId == instance.Id && response.TemplateItemId == itemIds[0])
            .ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal("CP", row.OptionCode);
        Assert.Equal("Corrección menor", row.Observations);
    }

    [Fact]
    public async Task SavingResponseOnSubmittedInstanceIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(2);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        foreach (var itemId in itemIds)
        {
            await PutJsonAsync<ResponseResponse>(
                $"/api/evaluations/{instance.Id}/responses/{itemId}",
                new { optionCode = "C" },
                technicianToken);
        }

        await PostJsonAsync<InstanceResponse>($"/api/evaluations/{instance.Id}/submit", null, technicianToken);

        using var response = await SendAsync(HttpMethod.Put, $"/api/evaluations/{instance.Id}/responses/{itemIds[0]}", new { optionCode = "IT" }, technicianToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ProgressReflectsAnsweredOverApplicableQuestions()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(4);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[0]}", new { optionCode = "C" }, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[1]}", new { optionCode = "NA" }, technicianToken);

        var progress = await GetJsonAsync<ProgressResponse>($"/api/evaluations/{instance.Id}", technicianToken);

        Assert.Equal(4, progress.TotalQuestions);
        Assert.Equal(2, progress.AnsweredQuestions);
        Assert.Equal(50m, progress.ProgressPercentage);
    }

    [Fact]
    public async Task SubmitLocksInstanceAndTransitionsCaseToPendingReport()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(2);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[0]}", new { optionCode = "C" }, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[1]}", new { optionCode = "CP" }, technicianToken);

        var submitted = await PostJsonAsync<InstanceResponse>($"/api/evaluations/{instance.Id}/submit", null, technicianToken);

        Assert.Equal("SUBMITTED", submitted.Status);
        Assert.NotNull(submitted.SubmittedAt);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var result = await context.EvaluationResults.SingleAsync(value => value.EvaluationInstanceId == instance.Id);
            Assert.Equal(75m, result.BpmPercentage);
            Assert.Equal("CAL-3", result.QualificationCode);
            Assert.Equal("Condiciones regulares", result.Classification);
            Assert.True(result.RiskCalculationId > 0);
        }

        var updatedCase = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("PENDING_REPORT", updatedCase.Status);

        using var secondSubmit = await SendAsync(HttpMethod.Post, $"/api/evaluations/{instance.Id}/submit", null, technicianToken);
        Assert.Equal(HttpStatusCode.Conflict, secondSubmit.StatusCode);
    }

    [Fact]
    public async Task ResultEndpointReturnsTheSnapshotOfTheSubmittedEvaluation()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(2);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        using (var beforeSubmit = await SendAsync(HttpMethod.Get, $"/api/evaluations/{instance.Id}/result", null, technicianToken))
            Assert.Equal(HttpStatusCode.NotFound, beforeSubmit.StatusCode);

        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[0]}", new { optionCode = "C" }, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[1]}", new { optionCode = "CP" }, technicianToken);
        await PostJsonAsync<InstanceResponse>($"/api/evaluations/{instance.Id}/submit", null, technicianToken);

        var result = await GetJsonAsync<ResultResponse>($"/api/evaluations/{instance.Id}/result", technicianToken);

        Assert.Equal(caseId, result.CaseId);
        Assert.Equal(1.5m, result.BpmPoints);
        Assert.Equal(2m, result.BpmDenominator);
        Assert.Equal(75m, result.BpmPercentage);
        Assert.Equal("CAL-3", result.QualificationCode);
        Assert.Equal("Condiciones regulares", result.Classification);
        Assert.Equal(0, result.CriticalCount);
        Assert.Equal(0, result.MajorCount);
        Assert.Equal(0, result.MinorCount);
        Assert.Empty(result.NonConformities);
        // 1.67 (banda BPM tercera opción) x 0.56 + 1.00 x 0.44 = 1.3752 de riesgo del establecimiento,
        // por 2 de riesgo del producto (Grasa láctea, microbiológico BAJO) = 2.750: banda [1, 3.6] -> 12 meses.
        Assert.Equal(2m, result.ProductRisk);
        Assert.Equal(1.3752m, result.EstablishmentRisk);
        Assert.Equal(2.75m, result.TotalRisk);
        Assert.Equal("Riesgo bajo", result.RiskLevel);
        Assert.Equal(12, result.FrequencyMonths);

        // El coordinador que da seguimiento al expediente también lo consulta.
        var seenByCoordinator = await GetJsonAsync<ResultResponse>($"/api/evaluations/{instance.Id}/result", coordinatorToken);
        Assert.Equal(result.EvaluationResultId, seenByCoordinator.EvaluationResultId);
    }

    [Fact]
    public async Task StoredResultDoesNotChangeWhenRiskRulesChangeAfterwards()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(2);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[0]}", new { optionCode = "C" }, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[1]}", new { optionCode = "CP" }, technicianToken);
        await PostJsonAsync<InstanceResponse>($"/api/evaluations/{instance.Id}/submit", null, technicianToken);

        var original = await GetJsonAsync<ResultResponse>($"/api/evaluations/{instance.Id}/result", technicianToken);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var version = await context.RiskRuleVersions.OrderByDescending(value => value.Version).FirstAsync();
            var factorIds = await context.StructuralRiskFactors
                .Where(value => value.RuleVersionId == version.Id).Select(value => value.Id).ToListAsync();
            foreach (var option in await context.StructuralRiskOptions.Where(value => factorIds.Contains(value.FactorId)).ToListAsync())
                option.Score += 5m;
            foreach (var band in await context.InspectionFrequencyMatrices.Where(value => value.RuleVersionId == version.Id).ToListAsync())
                band.FrequencyMonths = 1;
            await context.SaveChangesAsync();
        }

        var afterRuleChange = await GetJsonAsync<ResultResponse>($"/api/evaluations/{instance.Id}/result", technicianToken);

        Assert.Equal(original.EvaluationResultId, afterRuleChange.EvaluationResultId);
        Assert.Equal(original.BpmPercentage, afterRuleChange.BpmPercentage);
        Assert.Equal(original.EstablishmentRisk, afterRuleChange.EstablishmentRisk);
        Assert.Equal(original.TotalRisk, afterRuleChange.TotalRisk);
        Assert.Equal(12, afterRuleChange.FrequencyMonths);
    }

    [Fact]
    public async Task StoredResultRejectsAnyLaterModificationOrDeletion()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(2);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[0]}", new { optionCode = "C" }, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[1]}", new { optionCode = "CP" }, technicianToken);
        await PostJsonAsync<InstanceResponse>($"/api/evaluations/{instance.Id}/submit", null, technicianToken);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();

        var stored = await context.EvaluationResults.SingleAsync(value => value.EvaluationInstanceId == instance.Id);
        stored.BpmPercentage = 100m;
        var modification = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.SaveChangesAsync());
        Assert.Contains("fotografía inmutable", modification.Message, StringComparison.Ordinal);

        context.ChangeTracker.Clear();
        var reloaded = await context.EvaluationResults.SingleAsync(value => value.EvaluationInstanceId == instance.Id);
        context.EvaluationResults.Remove(reloaded);
        var deletion = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.SaveChangesAsync());
        Assert.Contains("fotografía inmutable", deletion.Message, StringComparison.Ordinal);

        // El porcentaje sigue siendo el calculado en el envío: el intento no dejó rastro.
        context.ChangeTracker.Clear();
        var unchanged = await context.EvaluationResults.AsNoTracking()
            .SingleAsync(value => value.EvaluationInstanceId == instance.Id);
        Assert.Equal(75m, unchanged.BpmPercentage);
    }

    [Fact]
    public async Task SubmitRejectsAnIncompleteEvaluationWithoutChangingItsState()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var itemIds = await CreatePublishedTemplateAsync(2);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);
        await PutJsonAsync<ResponseResponse>($"/api/evaluations/{instance.Id}/responses/{itemIds[0]}", new { optionCode = "C" }, technicianToken);

        using var response = await SendAsync(HttpMethod.Post, $"/api/evaluations/{instance.Id}/submit", null, technicianToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var unchanged = await GetJsonAsync<ProgressResponse>($"/api/evaluations/{instance.Id}", technicianToken);
        Assert.Equal("IN_PROGRESS", unchanged.Status);
    }

    [Fact]
    public async Task GenericTransitionCannotSubmitEvaluationWithoutLockingInstance()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        await CreatePublishedTemplateAsync(1);
        var caseId = await CreateScheduledCaseAsync(coordinatorToken, technicianId);
        var instance = await PostJsonAsync<InstanceResponse>($"/api/cases/{caseId}/evaluations", null, technicianToken);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/transition", new
        {
            newStatus = "PENDING_REPORT",
            reason = "Intento de omitir el envío de la evaluación"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var unchanged = await GetJsonAsync<ProgressResponse>($"/api/evaluations/{instance.Id}", technicianToken);
        Assert.Equal("IN_PROGRESS", unchanged.Status);
        Assert.Null(unchanged.SubmittedAt);
    }

    private async Task<int> CreateScheduledCaseAsync(string coordinatorToken, Guid technicianId)
    {
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", coordinatorToken));
        var created = await PostJsonAsync<CaseResponse>("/api/cases/institutional", new
        {
            companyId = company.Id,
            reason = $"Caso de prueba de evaluación {Guid.NewGuid():N}"
        }, coordinatorToken);

        await PostJsonAsync<object>($"/api/cases/{created.Id}/assign", new
        {
            technicianId,
            reason = "Asignación previa a la evaluación"
        }, coordinatorToken);

        var offsetDays = Interlocked.Increment(ref _scheduleOffsetDays);
        await PostJsonAsync<object>($"/api/cases/{created.Id}/schedule", new
        {
            scheduledFor = DateTimeOffset.UtcNow.Date.AddDays(30 + offsetDays).AddHours(9),
            reason = "Programación previa a la evaluación"
        }, coordinatorToken);

        return created.Id;
    }

    /// <summary>
    /// Crea, seedea bandas, publica y <strong>activa</strong> una plantilla nueva de prueba, para que
    /// <c>StartAsync</c> la use (desde el arreglo de la plantilla activa, ya no elige "la publicada más
    /// recientemente"). La mayoría de las pruebas de este archivo quieren exactamente eso: su propia
    /// plantilla recién creada, aislada de las demás. La única excepción es la prueba de activación
    /// explícita, que usa <see cref="CreateAndPublishTemplateAsync"/> directamente para poder dejar una
    /// plantilla publicada sin activar.
    /// </summary>
    private async Task<List<int>> CreatePublishedTemplateAsync(int questionCount) =>
        (await CreatePublishedTemplateWithIdAsync(questionCount)).ItemIds;

    private async Task<(int TemplateId, List<int> ItemIds)> CreatePublishedTemplateWithIdAsync(int questionCount)
    {
        var (templateId, itemIds) = await CreateAndPublishTemplateAsync(questionCount);
        var adminToken = await LoginAsync("admin@ebr.local");
        await PostJsonAsync<ActiveTemplateResponse>($"/api/evaluation-templates/{templateId}/activate", null, adminToken);
        return (templateId, itemIds);
    }

    /// <summary>Crea, seedea bandas y publica una plantilla de prueba, pero deliberadamente no la activa.</summary>
    private async Task<(int TemplateId, List<int> ItemIds)> CreateAndPublishTemplateAsync(int questionCount)
    {
        await EnsureRiskCatalogAsync();
        var adminToken = await LoginAsync("admin@ebr.local");
        var template = await PostJsonAsync<TemplateResponse>("/api/evaluation-templates", new
        {
            name = $"Plantilla de prueba {Guid.NewGuid():N}"
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

        // Las opciones evaluables (C/CP/IT/NA) ya las siembra automáticamente
        // `EvaluationTemplateEndpoints.CreateAsync` al crear la plantilla (contrato fijo de
        // `BpmResponseOptions`); aquí solo faltan las bandas de calificación, que sí son específicas de
        // cada plantilla y no tienen valor por defecto.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();

            // Las bandas de calificación son las de la ficha oficial (BpmTemplateData): el resultado
            // clasifica el porcentaje contra lo que declara la plantilla, no contra umbrales fijos.
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

        return (template.Id, itemIds);
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
            FullName = "Técnico adicional de prueba",
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

    private sealed record LoginResponse(string AccessToken);
    private sealed record CompanyResponse(int Id);
    private sealed record CaseResponse(int Id, int CompanyId, string SourceType, string Status);
    private sealed record MeResponse(Guid Id, string Email, string FullName, string Role);
    private sealed record TemplateResponse(int Id, string Name, int Version, string Status);
    private sealed record TemplateItemResponse(int Id, int TemplateId, string Code);
    private sealed record ActiveTemplateResponse(int? TemplateId, DateTimeOffset? ActivatedAt);

    private sealed record InstanceResponse(
        int Id, int CaseId, int TemplateId, string Status,
        DateTimeOffset StartedAt, Guid StartedBy, DateTimeOffset? SubmittedAt, Guid? SubmittedBy);

    private sealed record ResponseResponse(
        int Id, int EvaluationInstanceId, int TemplateItemId, string OptionCode,
        string Observations, string Comments, DateTimeOffset SavedAt, Guid SavedBy);

    private sealed record ResultResponse(
        int EvaluationResultId, int EvaluationInstanceId, int CaseId,
        decimal BpmPoints, decimal BpmDenominator, decimal BpmPercentage,
        string QualificationCode, string Classification, decimal BpmRiskScore,
        int CriticalCount, int MajorCount, int MinorCount,
        int RiskCalculationId, decimal ProductRisk, decimal EstablishmentRisk, decimal TotalRisk,
        string RiskLevel, int FrequencyMonths, DateTimeOffset CalculatedAt,
        List<ResultNonConformityResponse> NonConformities);

    private sealed record ResultNonConformityResponse(
        int Id, int EvaluationResponseId, int GuidanceCriterionId, string ItemCode, string CriterionCode, string Severity);

    private sealed record ProgressResponse(
        int Id, int CaseId, int TemplateId, string Status,
        DateTimeOffset StartedAt, Guid StartedBy, DateTimeOffset? SubmittedAt, Guid? SubmittedBy,
        int TotalQuestions, int AnsweredQuestions, decimal? ProgressPercentage);
}
