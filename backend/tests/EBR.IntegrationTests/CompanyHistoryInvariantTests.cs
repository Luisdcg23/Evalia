using EBR.Domain.Companies;
using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EBR.IntegrationTests;

public sealed class CompanyHistoryInvariantTests
{
    [Fact]
    public async Task ExistingHistoryEntryCannotBeModified()
    {
        await using var context = await SeedAsync();
        var entry = await context.CompanyHistoryEntries.SingleAsync();
        entry.ChangesJson = "{}";

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ExistingHistoryEntryCannotBeDeleted()
    {
        await using var context = await SeedAsync();
        var entry = await context.CompanyHistoryEntries.SingleAsync();
        context.CompanyHistoryEntries.Remove(entry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task NewHistoryEntriesCanStillBeAppended()
    {
        await using var context = await SeedAsync();
        context.CompanyHistoryEntries.Add(new CompanyHistoryEntry
        {
            CompanyId = 1,
            ChangedBy = Guid.NewGuid(),
            ChangesJson = "{\"tradeName\":{\"old\":\"A\",\"new\":\"B\"}}"
        });

        await context.SaveChangesAsync();

        Assert.Equal(2, await context.CompanyHistoryEntries.CountAsync());
    }

    private static async Task<EbrDbContext> SeedAsync()
    {
        var context = new EbrDbContext(new DbContextOptionsBuilder<EbrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Companies.Add(new Company { Id = 1, LegalName = "Historial SRL", Rnc = "999000111", TradeName = "Historial" });
        context.CompanyHistoryEntries.Add(new CompanyHistoryEntry
        {
            Id = 1,
            CompanyId = 1,
            ChangedBy = Guid.NewGuid(),
            ChangesJson = "{\"address\":{\"old\":\"\",\"new\":\"Calle 1\"}}"
        });
        await context.SaveChangesAsync();
        return context;
    }
}
