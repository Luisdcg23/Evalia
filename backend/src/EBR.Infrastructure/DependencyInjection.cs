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
        }

        services.AddSingleton<IRiskFormulaService, RiskFormulaService>();

        return services;
    }
}
