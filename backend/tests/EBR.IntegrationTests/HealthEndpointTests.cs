using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EBR.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly EbrApiFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests(EbrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetHealthReturnsOkWithHealthyStatus()
    {
        using var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseHealthReturnsServiceUnavailableWhenPostgresCannotBeReached()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:EbrDatabase"] = "Host=127.0.0.1;Port=1;Database=ebr_bpm;Username=test;Password=test;Timeout=1"
                });
            });
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/database");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Unhealthy", body, StringComparison.Ordinal);
    }
}
