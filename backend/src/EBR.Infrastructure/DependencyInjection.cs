using EBR.Application.Identity;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EBR.Infrastructure.Risk;
using EBR.Application.Risk;
using EBR.Application.Evaluations;
using EBR.Application.Evidence;
using EBR.Application.Reports;
using EBR.Infrastructure.Evaluations;
using EBR.Infrastructure.Evidence;
using EBR.Infrastructure.Reports;

namespace EBR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("EbrDatabase");
        if (environment.IsEnvironment("Testing"))
        {
            services.AddDbContext<EbrDbContext>(options =>
                options.UseInMemoryDatabase(configuration["Testing:DatabaseName"] ?? "EBR.Tests"));
        }
        else if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<EbrDbContext>(options => options.UseNpgsql(connectionString));
        }

        var persistenceConfigured = environment.IsEnvironment("Testing") ||
            !string.IsNullOrWhiteSpace(connectionString);
        if (persistenceConfigured)
        {
            services
                .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<EbrDbContext>();

            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32,
                    "Jwt:SigningKey debe tener al menos 32 caracteres")
                .ValidateOnStart();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IRiskCalculationService, RiskCalculationService>();
            services.AddScoped<IEvaluationSubmissionService, EvaluationSubmissionService>();
            services.AddScoped<RiskRuleVersionProvisioner>();
        }

        services.AddSingleton<IRiskFormulaService, RiskFormulaService>();

        // Licencia Community de QuestPDF, fijada al arranque para la generación del PDF oficial (RF-19).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddSingleton<IOfficialReportRenderer, OfficialReportRenderer>();

        // Almacenamiento de evidencias: la abstracción es la misma para todos los entornos y solo
        // cambia la implementación. Sin extremo de MinIO configurado se usa el sistema de archivos
        // local, que reproduce la misma semántica de bucket sin exigir un servicio adicional.
        services.AddOptions<EvidenceStorageOptions>()
            .Bind(configuration.GetSection(EvidenceStorageOptions.SectionName));
        var evidenceEndpoint = configuration[$"{EvidenceStorageOptions.SectionName}:Endpoint"];
        if (string.IsNullOrWhiteSpace(evidenceEndpoint))
            services.AddSingleton<IEvidenceStorage, FileSystemEvidenceStorage>();
        else
            services.AddSingleton<IEvidenceStorage, MinioEvidenceStorage>();

        return services;
    }
}
