using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class EvaluationTemplateEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public EvaluationTemplateEndpointTests(EbrApiFactory factory) => _client = factory.CreateClient();

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
        var token = await LoginAsync("admin@ebr.local");
        var template = await PostAsync<TemplateResponse>("/api/evaluation-templates", new { name = "Plantilla versionada" }, token);
        await PostAsync<ItemResponse>($"/api/evaluation-templates/{template.Id}/items", new
        {
            code = "1",
            description = "Higiene",
            itemType = "CHAPTER",
            order = 1
        }, token);
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
    private sealed record ItemResponse(
        int Id,
        int? ParentId,
        string Code,
        string Description,
        decimal? Weight,
        bool IsCritical);
}
