using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EBR.Infrastructure.Persistence;

public sealed class EbrDbContextFactory : IDesignTimeDbContextFactory<EbrDbContext>
{
    public EbrDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__EbrDatabase") ??
            "Host=localhost;Port=5432;Database=ebr_bpm;Username=postgres;Password=local-configuration-required";
        var options = new DbContextOptionsBuilder<EbrDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new EbrDbContext(options);
    }
}
