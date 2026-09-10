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

public sealed class CaseAssignmentEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public CaseAssignmentEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CoordinatorAssignsActiveTechnicianToPendingCase()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");

        var assignment = await PostJsonAsync<AssignmentResponse>($"/api/cases/{caseId}/assign", new
        {
            technicianId,
            reason = "Primera asignación del expediente"
        }, coordinatorToken);

        Assert.Equal(technicianId, assignment.TechnicianId);
        Assert.True(assignment.IsCurrent);

        var updatedCase = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("ASSIGNED", updatedCase.Status);

        var history = await GetJsonAsync<List<AssignmentResponse>>($"/api/cases/{caseId}/assignments", coordinatorToken);
        var current = Assert.Single(history);
        Assert.True(current.IsCurrent);
        Assert.Equal(technicianId, current.TechnicianId);
    }

    [Fact]
    public async Task AssigningUserWithoutEvaluatorRoleIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var adminId = await GetUserIdAsync("admin@ebr.local");

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/assign", new
        {
            technicianId = adminId,
            reason = "Intento inválido"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stillPending = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("PENDING_ASSIGNMENT", stillPending.Status);
    }

    [Fact]
    public async Task AssigningUnapprovedEvaluatorIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var pendingTechnicianId = await CreateEvaluatorUserAsync(
            $"tecnico-pendiente-{Guid.NewGuid():N}@ebr.local", UserApprovalStatus.PendingValidation);

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/assign", new
        {
            technicianId = pendingTechnicianId,
            reason = "Intento con técnico no aprobado"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stillPending = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("PENDING_ASSIGNMENT", stillPending.Status);
    }

    [Fact]
    public async Task AssignmentRejectsNullOrOversizedReason()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");

        foreach (var reason in new string?[] { null, new('x', 1001) })
        {
            var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
            using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/assign", new
            {
                technicianId,
                reason
            }, coordinatorToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task UnauthorizedRoleCannotAssignTechnician()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var technicianToken = await LoginAsync("tecnico@ebr.local");

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/assign", new
        {
            technicianId,
            reason = "Intento no autorizado"
        }, technicianToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorCannotAssignTechnician()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var adminToken = await LoginAsync("admin@ebr.local");

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/assign", new
        {
            technicianId,
            reason = "Intento por administrador"
        }, adminToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignmentHistoryForMissingCaseReturnsNotFound()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        using var response = await SendAsync(HttpMethod.Get, "/api/cases/2147483647/assignments", null, coordinatorToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PersistedAssignmentHistoryCannotBeEdited()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");
        var created = await PostJsonAsync<AssignmentResponse>($"/api/cases/{caseId}/assign", new
        {
            technicianId,
            reason = "Motivo original"
        }, coordinatorToken);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
        var assignment = await context.CaseAssignments.SingleAsync(value => value.Id == created.Id);
        assignment.Reason = "Motivo alterado";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("historial de asignaciones", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReassigningAssignedCaseMarksPreviousAssignmentAsHistoricalWithoutChangingStatus()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var firstTechnicianId = await GetUserIdAsync("tecnico@ebr.local");
        var secondTechnicianId = await CreateEvaluatorUserAsync(
            $"tecnico-segundo-{Guid.NewGuid():N}@ebr.local", UserApprovalStatus.Approved);

        await PostJsonAsync<AssignmentResponse>($"/api/cases/{caseId}/assign", new
        {
            technicianId = firstTechnicianId,
            reason = "Primera asignación"
        }, coordinatorToken);

        var reassignment = await PostJsonAsync<AssignmentResponse>($"/api/cases/{caseId}/assign", new
        {
            technicianId = secondTechnicianId,
            reason = "Reasignación por disponibilidad"
        }, coordinatorToken);

        Assert.Equal(secondTechnicianId, reassignment.TechnicianId);
        Assert.True(reassignment.IsCurrent);

        var caseAfterReassignment = await GetJsonAsync<CaseResponse>($"/api/cases/{caseId}", coordinatorToken);
        Assert.Equal("ASSIGNED", caseAfterReassignment.Status);

        var history = await GetJsonAsync<List<AssignmentResponse>>($"/api/cases/{caseId}/assignments", coordinatorToken);
        Assert.Equal(2, history.Count);
        Assert.Equal(firstTechnicianId, history[0].TechnicianId);
        Assert.False(history[0].IsCurrent);
        Assert.Equal(secondTechnicianId, history[1].TechnicianId);
        Assert.True(history[1].IsCurrent);
        Assert.Single(history, item => item.IsCurrent);
    }

    [Fact]
    public async Task ReassigningCancelledCaseIsRejected()
    {
        var coordinatorToken = await LoginAsync("coordinador@ebr.local");
        var caseId = await CreateInstitutionalCaseAsync(coordinatorToken);
        var technicianId = await GetUserIdAsync("tecnico@ebr.local");

        await PostJsonAsync<AssignmentResponse>($"/api/cases/{caseId}/assign", new
        {
            technicianId,
            reason = "Primera asignación"
        }, coordinatorToken);

        using var transitionResponse = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/transition", new
        {
            newStatus = "CANCELLED",
            reason = "El establecimiento cerró operaciones"
        }, coordinatorToken);
        transitionResponse.EnsureSuccessStatusCode();

        using var response = await SendAsync(HttpMethod.Post, $"/api/cases/{caseId}/assign", new
        {
            technicianId,
            reason = "Intento de reasignación sobre caso cancelado"
        }, coordinatorToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<int> CreateInstitutionalCaseAsync(string coordinatorToken)
    {
        var company = Assert.Single(await GetJsonAsync<List<CompanyResponse>>("/api/companies", coordinatorToken));
        var created = await PostJsonAsync<CaseResponse>("/api/cases/institutional", new
        {
            companyId = company.Id,
            reason = $"Caso de prueba de asignación {Guid.NewGuid():N}"
        }, coordinatorToken);
        return created.Id;
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        var token = await LoginAsync(email);
        var me = await GetJsonAsync<MeResponse>("/api/users/me", token);
        return me.Id;
    }

    private async Task<Guid> CreateEvaluatorUserAsync(string email, UserApprovalStatus approvalStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Técnico de Prueba",
            ApprovalStatus = approvalStatus
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
    private sealed record CompanyResponse(int Id);
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
}
