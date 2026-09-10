using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EBR.Infrastructure.Identity;
using EBR.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.IntegrationTests;

public sealed class CaseScheduleEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public CaseScheduleEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SchedulingAssignedCaseTransitionsToScheduledAndCreatesCurrentSchedule()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var scheduledFor = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

        var schedule = await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor,
            reason = "Primera programación de la visita",
            priority = "alta",
            observations = "Llevar equipo de muestreo"
        }, coordinatorToken);

        Assert.Equal(technicianId, schedule.TechnicianId);
        Assert.True(schedule.IsCurrent);
        Assert.Equal(scheduledFor, schedule.ScheduledFor);

        var updatedCase = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("SCHEDULED", updatedCase.Status);

        var history = await GetJsonAsync<List<ScheduleResponse>>($"/api/cases/{caseId}/schedules", coordinatorToken);
        var current = Assert.Single(history);
        Assert.True(current.IsCurrent);
    }

    [Fact]
    public async Task SchedulingCaseWithoutAssignedTechnicianIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            reason = "Intento sin técnico asignado"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stillPending = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("PENDING_ASSIGNMENT", stillPending.Status);
    }

    [Fact]
    public async Task SchedulingWithoutDateIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/schedule", new
        {
            reason = "Intento sin fecha"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SchedulingMoreThanFiveMinutesInThePastIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = DateTimeOffset.UtcNow.AddMinutes(-6),
            reason = "Intento con fecha pasada"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ScheduleRejectsFieldsLongerThanDatabaseConstraints()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = new DateTimeOffset(2027, 3, 1, 9, 0, 0, TimeSpan.Zero),
            reason = new string('m', 1001),
            priority = new string('p', 21),
            observations = new string('o', 2001)
        }, coordinatorToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var unchanged = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("ASSIGNED", unchanged.Status);
    }

    [Fact]
    public async Task ReschedulingMarksPreviousScheduleAsHistoricalWithoutChangingCaseStatus()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var firstDate = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
        var secondDate = new DateTimeOffset(2026, 10, 7, 14, 0, 0, TimeSpan.Zero);

        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = firstDate,
            reason = "Programación inicial"
        }, coordinatorToken);

        var rescheduled = await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/reschedule", new
        {
            scheduledFor = secondDate,
            reason = "Reprogramación por disponibilidad de la empresa"
        }, coordinatorToken);

        Assert.Equal(secondDate, rescheduled.ScheduledFor);
        Assert.True(rescheduled.IsCurrent);

        var caseAfterReschedule = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("SCHEDULED", caseAfterReschedule.Status);

        var history = await GetJsonAsync<List<ScheduleResponse>>($"/api/cases/{caseId}/schedules", coordinatorToken);
        Assert.Equal(2, history.Count);
        Assert.False(history[0].IsCurrent);
        Assert.Equal(firstDate, history[0].ScheduledFor);
        Assert.True(history[1].IsCurrent);
        Assert.Equal(secondDate, history[1].ScheduledFor);
    }

    [Fact]
    public async Task SchedulingOverlappingTimeForSameTechnicianIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");

        var firstCaseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var firstDate = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{firstCaseId}/schedule", new
        {
            scheduledFor = firstDate,
            reason = "Primer caso programado"
        }, coordinatorToken);

        var secondCaseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var overlappingDate = firstDate.AddHours(1);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{secondCaseId}/schedule", new
        {
            scheduledFor = overlappingDate,
            reason = "Intento de programación solapada"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var secondCase = await GetJsonAsync<CaseResponse>($"/api/cases/{secondCaseId}", coordinatorToken);
        Assert.Equal("ASSIGNED", secondCase.Status);
    }

    [Fact]
    public async Task ReschedulingIntoOverlappingTimeForSameTechnicianIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");

        var firstCaseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var firstDate = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{firstCaseId}/schedule", new
        {
            scheduledFor = firstDate,
            reason = "Primer caso programado"
        }, coordinatorToken);

        var secondCaseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var farDate = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{secondCaseId}/schedule", new
        {
            scheduledFor = farDate,
            reason = "Segundo caso en horario distinto"
        }, coordinatorToken);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{secondCaseId}/reschedule", new
        {
            scheduledFor = firstDate.AddHours(1),
            reason = "Intento de reprogramación solapada"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CancellingScheduleMakesItNotCurrentAndRejectsDoubleCancellation()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero),
            reason = "Programación a cancelar"
        }, coordinatorToken);

        var cancelled = await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/cancel-schedule", new
        {
            reason = "La empresa solicitó posponer indefinidamente"
        }, coordinatorToken);

        Assert.False(cancelled.IsCurrent);
        Assert.NotNull(cancelled.CancelledAt);

        using var secondCancellation = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/cancel-schedule", new
        {
            reason = "Segundo intento de cancelación"
        }, coordinatorToken);
        Assert.Equal(HttpStatusCode.Conflict, secondCancellation.StatusCode);

        var history = await GetJsonAsync<List<ScheduleResponse>>($"/api/cases/{caseId}/schedules", coordinatorToken);
        Assert.Single(history);
        Assert.False(history[0].IsCurrent);
    }

    [Fact]
    public async Task CancelledScheduleReturnsCaseToAssignedAndCanBeScheduledAgain()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = new DateTimeOffset(2027, 1, 7, 9, 0, 0, TimeSpan.Zero),
            reason = "Programación inicial"
        }, coordinatorToken);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/cancel-schedule", new
        {
            reason = "Cambio solicitado por el establecimiento"
        }, coordinatorToken);

        var afterCancellation = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("ASSIGNED", afterCancellation.Status);
        var replacement = await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = new DateTimeOffset(2027, 1, 8, 9, 0, 0, TimeSpan.Zero),
            reason = "Nueva programación"
        }, coordinatorToken);
        Assert.True(replacement.IsCurrent);
    }

    [Fact]
    public async Task GenericTransitionCannotStartEvaluationWithoutAnInstance()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = new DateTimeOffset(2027, 2, 1, 9, 0, 0, TimeSpan.Zero),
            reason = "Programación previa"
        }, coordinatorToken);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/transition", new
        {
            newStatus = "IN_EVALUATION",
            reason = "Intento de omitir la creación de instancia"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var current = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("SCHEDULED", current.Status);
    }

    [Fact]
    public async Task CancellingCaseRetiresCurrentScheduleAndAssignment()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor = new DateTimeOffset(2027, 2, 2, 9, 0, 0, TimeSpan.Zero),
            reason = "Programación previa"
        }, coordinatorToken);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/transition", new
        {
            newStatus = "CANCELLED",
            reason = "Cierre definitivo del establecimiento"
        }, coordinatorToken);
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        Assert.False(await context.CaseSchedules.AnyAsync(value => value.CaseId == caseId && value.IsCurrent));
        Assert.False(await context.CaseAssignments.AnyAsync(value => value.CaseId == caseId && value.IsCurrent));
    }

    [Fact]
    public async Task AgendaQueryReturnsCompanyAddressDateAndCaseStatusWithinRange()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var scheduledFor = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor,
            reason = "Programación para consulta de agenda"
        }, coordinatorToken);

        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", coordinatorToken));

        var from = scheduledFor.AddDays(-1).ToString("O");
        var to = scheduledFor.AddDays(1).ToString("O");
        var agenda = await GetJsonAsync<List<AgendaEntryResponse>>(
            $"/api/cases/schedule?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}",
            coordinatorToken);

        var entry = Assert.Single(agenda, item => item.CaseId == caseId);
        Assert.Equal(company.Id, entry.CompanyId);
        Assert.Equal(company.Address, entry.CompanyAddress);
        Assert.Equal(scheduledFor, entry.ScheduledFor);
        Assert.Equal("SCHEDULED", entry.Status);
    }

    [Fact]
    public async Task AgendaRejectsInvertedRangeAndTreatsUpperBoundAsExclusive()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var caseId = await CreateAssignedCaseAsync(coordinatorToken, technicianId);
        var scheduledFor = new DateTimeOffset(2027, 3, 2, 9, 0, 0, TimeSpan.Zero);
        await PostJsonAsync<ScheduleResponse>($"/api/cases/{caseId}/schedule", new
        {
            scheduledFor,
            reason = "Límite superior de agenda"
        }, coordinatorToken);

        using var invalid = await SendAsync(HttpMethod.Get,
            $"/api/cases/schedule?from={Uri.EscapeDataString(scheduledFor.AddDays(1).ToString("O"))}&to={Uri.EscapeDataString(scheduledFor.ToString("O"))}",
            null, coordinatorToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var entries = await GetJsonAsync<List<AgendaEntryResponse>>(
            $"/api/cases/schedule?from={Uri.EscapeDataString(scheduledFor.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(scheduledFor.ToString("O"))}",
            coordinatorToken);
        Assert.DoesNotContain(entries, entry => entry.CaseId == caseId);
    }

    private async Task<int> CreateAssignedCaseAsync(string coordinatorToken, Guid technicianId)
    {
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        await PostJsonAsync<AssignmentResponse>($"/api/cases/{caseId}/assign", new
        {
            technicianId,
            reason = "Asignación previa a la programación"
        }, coordinatorToken);
        return caseId;
    }

    private async Task<int> CreateInstitutionalCaseAsync(string coordinatorToken)
    {
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", coordinatorToken));
        var created = await PostJsonAsync<CaseResponse>("/api/cases/institutional", new
        {
            companyId = company.Id,
            reason = $"Caso de prueba de programación {Guid.NewGuid():N}"
        }, coordinatorToken);
        return created.Id;
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        var token = await LoginAsync(email);
        var me = await GetJsonAsync<MeResponse>("/api/users/me", token);
        return me.Id;
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

    private async Task<T> PostJsonAsync<T>(string uri, object body, string token)
    {
        using var response = await SendAsync(HttpMethod.Post, uri, body, token);
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
    private sealed record CompanyResponse(int Id, string Address);
    private sealed record CaseResponse(int Id, int CompanyId, string SourceType, string Status);
    private sealed record MeResponse(Guid Id, string Email, string FullName, string Role);

    private sealed record AssignmentResponse(
        int Id,
        int CaseId,
        Guid TechnicianId,
        bool IsCurrent,
        DateTimeOffset AssignedAt,
        Guid AssignedBy,
        string Reason);

    private sealed record ScheduleResponse(
        int Id,
        int CaseId,
        Guid TechnicianId,
        DateTimeOffset ScheduledFor,
        string Priority,
        string Reason,
        string Observations,
        bool IsCurrent,
        DateTimeOffset CreatedAt,
        Guid ScheduledBy,
        DateTimeOffset? CancelledAt,
        Guid? CancelledBy,
        string? CancellationReason);

    private sealed record AgendaEntryResponse(
        int CaseId,
        int CompanyId,
        string CompanyName,
        string CompanyAddress,
        DateTimeOffset ScheduledFor,
        string Status,
        string Priority,
        Guid TechnicianId);
}
