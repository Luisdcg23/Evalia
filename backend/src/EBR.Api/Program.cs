using System.Text;
using EBR.Api.Endpoints;
using EBR.Api.Health;
using EBR.Infrastructure;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.ClearProviders();
}

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy =>
    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
var authenticationConfigured = !string.IsNullOrWhiteSpace(jwtSigningKey);
if (authenticationConfigured)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey!)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
    builder.Services.AddAuthorization();
}

builder.Services
    .AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["database"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("LocalFrontend");
app.UseRateLimiter();
if (authenticationConfigured)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => !registration.Tags.Contains("database")
});
app.MapHealthChecks("/health/database", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("database")
});
if (authenticationConfigured)
{
    app.MapAuthEndpoints();
    app.MapAdminEndpoints();
    app.MapUserEndpoints();
    app.MapCompanyEndpoints();
    app.MapCatalogEndpoints();
    app.MapRiskCatalogEndpoints();
    app.MapRiskEndpoints();
    app.MapEvaluationTemplateEndpoints();
    app.MapBpmRequestEndpoints();
    app.MapCaseEndpoints();
    app.MapRegulatoryOriginEndpoints();
    app.MapEvaluationInstanceEndpoints();
    app.MapEvaluationEvidenceEndpoints();
    app.MapEvaluationReportEndpoints();
}

if (app.Environment.IsEnvironment("Testing") ||
    (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("EbrDatabase"))))
{
    await using var scope = app.Services.CreateAsyncScope();
    if (scope.ServiceProvider.GetService<EbrDbContext>() is not null)
    {
        await DevelopmentDataSeeder.SeedAsync(scope.ServiceProvider, CancellationToken.None);
    }
}

app.Run();

public partial class Program;
