using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class AlertAndComplaintEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;
    public AlertAndComplaintEndpointTests(EbrApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task ProceedingAlertCreatesOneCaseWhileRejectedAlertCreatesNone()
    {
        var token = await LoginAsync();
        var company = Assert.Single(await GetAsync<List<IdResponse>>("/api/companies", token));
        var rejected = await PostAsync<IdResponse>("/api/alerts", new
        {
            alertNumber = $"LAPCH-{Guid.NewGuid():N}", receivedAt = DateTimeOffset.UtcNow,
            product = "Producto A", companyId = company.Id, description = "Análisis preventivo"
        }, token);
        await PostAsync<DecisionResponse>($"/api/alerts/{rejected.Id}/decision", new { result = "NOT_PROCEED", reason = "Sin evidencia" }, token);
        var proceeding = await PostAsync<IdResponse>("/api/alerts", new
        {
            alertNumber = $"LAPCH-{Guid.NewGuid():N}", receivedAt = DateTimeOffset.UtcNow,
            product = "Producto B", companyId = company.Id, description = "Hallazgo confirmado"
        }, token);
        var first = await PostAsync<DecisionResponse>($"/api/alerts/{proceeding.Id}/decision", new { result = "PROCEED", reason = "Requiere inspección" }, token);
        var repeated = await PostAsync<DecisionResponse>($"/api/alerts/{proceeding.Id}/decision", new { result = "PROCEED", reason = "Requiere inspección" }, token);

        Assert.Null((await GetAsync<List<CaseResponse>>("/api/cases", token)).SingleOrDefault(item => item.SourceType == "ALERT" && item.SourceReferenceId == rejected.Id));
        Assert.NotNull(first.CaseId);
        Assert.Equal(first.CaseId, repeated.CaseId);
    }

    [Fact]
    public async Task ReferredComplaintDoesNotCreateAnEvaluationCase()
    {
        var token = await LoginAsync();
        var complaint = await PostAsync<IdResponse>("/api/complaints", new
        {
            complaintType = "Etiquetado", receivedAt = DateTimeOffset.UtcNow,
            complainant = "Ciudadano", description = "Reporte para otro proceso"
        }, token);
        var decision = await PostAsync<DecisionResponse>($"/api/complaints/{complaint.Id}/decision", new
        {
            result = "REFERRED", reason = "Competencia de otra unidad"
        }, token);

        Assert.Equal("REFERRED", decision.Status);
        Assert.Null(decision.CaseId);
    }

    private async Task<string> LoginAsync()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "admin@ebr.local", password = "EbrLocal2026!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }
    private async Task<T> GetAsync<T>(string uri, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<T> PostAsync<T>(string uri, object body, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private sealed record LoginResponse(string AccessToken);
    private sealed record IdResponse(int Id);
    private sealed record CaseResponse(int SourceReferenceId, string SourceType);
    private sealed record DecisionResponse(string Status, int? CaseId);
}
