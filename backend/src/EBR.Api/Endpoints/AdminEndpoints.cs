using EBR.Domain.Identity;

namespace EBR.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/system-status", () => Results.Ok(new
        {
            status = "operational"
        }))
        .WithTags("Administración")
        .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator));

        return endpoints;
    }
}
