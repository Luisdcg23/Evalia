using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EBR.IntegrationTests;

public sealed class CompanySchemaTests
{
    [Fact]
    public void CompanyHistoryEntryIsMappedToItsOwnAppendOnlyTable()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var entity = context.Model.FindEntityType("EBR.Domain.Companies.CompanyHistoryEntry");
        Assert.NotNull(entity);
        Assert.Equal("Empresa_Historial", entity!.GetTableName());
    }

    [Fact]
    public void CompanyVersionTokenIsAConcurrencyToken()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var property = context.Model.FindEntityType("EBR.Domain.Companies.Company")!.FindProperty("VersionToken");
        Assert.NotNull(property);
        Assert.True(property!.IsConcurrencyToken);
    }

    [Fact]
    public void OnlyOneActiveRepresentativePerTypeIsEnforcedByAFilteredUniqueIndex()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var entity = context.Model.FindEntityType("EBR.Domain.Companies.CompanyRepresentative")!;
        var index = entity.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(["CompanyId", "RepresentativeType"]));
        Assert.True(index.IsUnique);
        Assert.NotNull(index.GetFilter());
    }

    [Theory]
    [InlineData("tr_empresa_historial_inmutable")]
    [InlineData("CREATE OR REPLACE FUNCTION fn_empresa_historial_inmutable")]
    public void MigrationDeliversTheHistoryImmutabilityTrigger(string fragment)
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains(fragment, sql);
    }
}
