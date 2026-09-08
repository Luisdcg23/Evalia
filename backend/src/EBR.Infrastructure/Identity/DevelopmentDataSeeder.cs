using EBR.Domain.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EBR.Domain.Companies;

namespace EBR.Infrastructure.Identity;

public static class DevelopmentDataSeeder
{
    private const string DevelopmentPassword = "EbrLocal2026!";

    private static readonly IReadOnlyList<(string Email, string Name, string Role)> Users =
    [
        ("admin@ebr.local", "Administrador EBR", SystemRoles.Administrator),
        ("empresa@ebr.local", "Administrador de Empresa", SystemRoles.CompanyAdministrator),
        ("delegado@ebr.local", "Usuario Delegado", SystemRoles.DelegateUser),
        ("coordinador@ebr.local", "Coordinador EBR", SystemRoles.Coordinator),
        ("tecnico@ebr.local", "Técnico Evaluador", SystemRoles.Evaluator)
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<EbrDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        var demoCompany = await context.Companies.SingleOrDefaultAsync(
            company => company.Rnc == "130000001",
            cancellationToken);
        if (demoCompany is null)
        {
            demoCompany = new Company
            {
                LegalName = "Empresa Demostración EBR SRL",
                Rnc = "130000001",
                TradeName = "Empresa EBR"
            };
            context.Companies.Add(demoCompany);
            await context.SaveChangesAsync(cancellationToken);
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in SystemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                EnsureSucceeded(result, $"crear rol {role}");
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var seed in Users)
        {
            if (await userManager.FindByEmailAsync(seed.Email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
                FullName = seed.Name,
                ApprovalStatus = UserApprovalStatus.Approved
            };
            var createResult = await userManager.CreateAsync(user, DevelopmentPassword);
            EnsureSucceeded(createResult, $"crear usuario {seed.Email}");
            var roleResult = await userManager.AddToRoleAsync(user, seed.Role);
            EnsureSucceeded(roleResult, $"asignar rol {seed.Role}");
        }

        foreach (var email in new[] { "empresa@ebr.local", "delegado@ebr.local" })
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null && !await context.CompanyUsers.AnyAsync(
                link => link.CompanyId == demoCompany.Id && link.UserId == user.Id,
                cancellationToken))
            {
                context.CompanyUsers.Add(new CompanyUser { CompanyId = demoCompany.Id, UserId = user.Id });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"No fue posible {operation}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
        }
    }
}
