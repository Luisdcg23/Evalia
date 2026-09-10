using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EBR.Domain.Companies;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EBR.IntegrationTests;

public sealed class OperationalReadEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public OperationalReadEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EvaluatorSeesOnlyCurrentlyAssignedCasesAndSchedule()
    {
        var token = await LoginAsync("tecnico@ebr.local");
        var technicianId = await UserIdAsync("tecnico@ebr.local");
        int visibleId, hiddenId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var companyId = await db.Companies.Select(x => x.Id).FirstAsync();
            var creator = await UserIdAsync("coordinador@ebr.local");
            var visible = NewCase(companyId, creator, CaseStatuses.Scheduled);
            var hidden = NewCase(companyId, creator, CaseStatuses.Assigned);
            db.InspectionCases.AddRange(visible, hidden); await db.SaveChangesAsync();
            visibleId = visible.Id; hiddenId = hidden.Id;
            db.CaseAssignments.AddRange(
                new CaseAssignment { CaseId = visibleId, TechnicianId = technicianId, AssignedBy = creator, Reason = "vigente" },
                new CaseAssignment { CaseId = hiddenId, TechnicianId = technicianId, AssignedBy = creator, Reason = "histórica", IsCurrent = false });
            db.CaseSchedules.Add(new CaseSchedule { CaseId = visibleId, TechnicianId = technicianId,
                ScheduledFor = DateTimeOffset.UtcNow.AddDays(1), ScheduledBy = creator, Reason = "visita" });
            await db.SaveChangesAsync();
        }

        var cases = await GetAsync<List<CaseRow>>("/api/me/cases", token);
        Assert.Contains(cases, x => x.Id == visibleId);
        Assert.DoesNotContain(cases, x => x.Id == hiddenId);
        var schedule = await GetAsync<List<ScheduleRow>>("/api/me/schedule", token);
        Assert.Contains(schedule, x => x.CaseId == visibleId);
    }

    [Fact]
    public async Task CompanyUserCannotSeeAnotherCompanyCases()
    {
        var token = await LoginAsync("empresa@ebr.local");
        int ownId, foreignId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var userId = await UserIdAsync("empresa@ebr.local");
            var ownCompanyId = await db.CompanyUsers.Where(x => x.UserId == userId).Select(x => x.CompanyId).SingleAsync();
            var foreign = new Company { LegalName = "Empresa aislada", TradeName = "Aislada", Rnc = $"9{Random.Shared.Next(10000000, 99999999)}" };
            db.Companies.Add(foreign); await db.SaveChangesAsync();
            var own = NewCase(ownCompanyId, userId, CaseStatuses.PendingAssignment);
            var other = NewCase(foreign.Id, userId, CaseStatuses.PendingAssignment);
            db.InspectionCases.AddRange(own, other); await db.SaveChangesAsync();
            ownId = own.Id; foreignId = other.Id;
        }
        var rows = await GetAsync<List<CaseRow>>("/api/me/cases", token);
        Assert.Contains(rows, x => x.Id == ownId);
        Assert.DoesNotContain(rows, x => x.Id == foreignId);
    }

    [Fact]
    public async Task CoordinatorGetsApprovedTechniciansWithRealLoad()
    {
        var token = await LoginAsync("coordinador@ebr.local");
        var rows = await GetAsync<List<TechnicianRow>>("/api/technicians", token);
        Assert.Contains(rows, x => x.Email == "tecnico@ebr.local" && x.ActiveCaseCount >= 0);
    }

    [Fact]
    public async Task NotificationsArePrivateAndReadIsIdempotent()
    {
        var technicianToken = await LoginAsync("tecnico@ebr.local");
        var companyToken = await LoginAsync("empresa@ebr.local");
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EbrDbContext>();
            var recipient = await UserIdAsync("tecnico@ebr.local");
            var item = new Notification { RecipientId = recipient, Type = NotificationTypes.CaseAssigned,
                Title = "Nuevo expediente", Message = "Se asignó un expediente." };
            db.Notifications.Add(item); await db.SaveChangesAsync(); id = item.Id;
        }
        var mine = await GetAsync<List<Notification>>("/api/notifications?unreadOnly=true", technicianToken);
        Assert.Contains(mine, x => x.Id == id);
        using var forbiddenByPrivacy = await SendAsync(HttpMethod.Patch, $"/api/notifications/{id}/read", companyToken);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenByPrivacy.StatusCode);
        using var first = await SendAsync(HttpMethod.Patch, $"/api/notifications/{id}/read", technicianToken);
        using var second = await SendAsync(HttpMethod.Patch, $"/api/notifications/{id}/read", technicianToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task EvaluatorCannotSearchGlobalHistory()
    {
        var token = await LoginAsync("tecnico@ebr.local");
        using var response = await SendAsync(HttpMethod.Get, "/api/cases/history/search", token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static InspectionCase NewCase(int companyId, Guid creator, string status) => new()
    { CompanyId = companyId, CreatedBy = creator, SourceType = "INSTITUTIONAL", SourceReferenceId = Random.Shared.Next(100000, 999999), Status = status };

    private async Task<string> LoginAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "EbrLocal2026!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await manager.FindByEmailAsync(email))!.Id;
    }

    private async Task<T> GetAsync<T>(string url, string token)
    {
        using var response = await SendAsync(HttpMethod.Get, url, token); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record CaseRow(int Id);
    private sealed record ScheduleRow(int CaseId);
    private sealed record TechnicianRow(string Email, int ActiveCaseCount);
}
