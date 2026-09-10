using EBR.Application.Evidence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EBR.IntegrationTests;

public sealed class EbrApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"EBR.Tests.{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILoggerProvider>();
            // Las evidencias se guardan en un bucket en memoria: la suite ejerce la misma abstracción
            // que producción sin depender de MinIO ni escribir binarios en disco.
            services.RemoveAll<IEvidenceStorage>();
            services.AddSingleton<IEvidenceStorage, InMemoryEvidenceStorage>();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:DatabaseName"] = _databaseName
            }));
    }
}
