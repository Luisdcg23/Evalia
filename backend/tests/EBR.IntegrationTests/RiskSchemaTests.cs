using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EBR.IntegrationTests;

public sealed class RiskSchemaTests
{
    [Theory]
    [InlineData("RiskScale", "Escala_Riesgo")]
    [InlineData("RiskScaleLevel", "Escala_Riesgo_Nivel")]
    [InlineData("FoodSubcategoryHazard", "Subcategoria_Alimento_Peligro")]
    [InlineData("RiskRuleVersion", "Version_Regla_Riesgo")]
    public void NormalizedRiskConceptsAreMapped(string name, string table)
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var entity = context.Model.FindEntityType($"EBR.Domain.RiskCatalogs.{name}");
        Assert.NotNull(entity);
        Assert.Equal(table, entity.GetTableName());
    }

    [Fact]
    public void MigrationDeliversCallableTransactionalRiskRoutine()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE OR REPLACE PROCEDURE sp_registrar_calculo_riesgo", sql);
    }
}
