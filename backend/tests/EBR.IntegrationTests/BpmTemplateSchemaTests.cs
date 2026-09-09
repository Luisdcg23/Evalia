using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EBR.IntegrationTests;

public sealed class BpmTemplateSchemaTests
{
    [Theory]
    [InlineData("EvaluationResponseOption", "Plantilla_Evaluacion_Opcion")]
    [InlineData("EvaluationGuidanceCriterion", "Plantilla_Evaluacion_Criterio")]
    [InlineData("EvaluationQualificationRule", "Plantilla_Evaluacion_Regla")]
    [InlineData("EvaluationImportBatch", "Plantilla_Importacion_Lote")]
    [InlineData("EvaluationImportRow", "Plantilla_Importacion_Fila")]
    public void TemplateCatalogsAreMapped(string name, string table)
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var entity = context.Model.FindEntityType($"EBR.Domain.Evaluations.{name}");
        Assert.NotNull(entity);
        Assert.Equal(table, entity.GetTableName());
    }

    [Fact]
    public void NormativeTextsUseUnboundedColumns()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var description = context.Model
            .FindEntityType("EBR.Domain.Evaluations.EvaluationTemplateItem")!
            .FindProperty("Description")!;
        Assert.Equal("text", description.GetColumnType());
        Assert.Null(description.GetMaxLength());
    }

    [Theory]
    [InlineData("CREATE OR REPLACE FUNCTION fn_plantilla_arbol")]
    [InlineData("CREATE OR REPLACE PROCEDURE sp_importar_plantilla_bpm")]
    [InlineData("CREATE OR REPLACE PROCEDURE sp_publicar_plantilla")]
    [InlineData("tr_plantilla_publicada_inmutable")]
    public void MigrationDeliversTheTemplateRoutines(string fragment)
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains(fragment, sql);
    }
}
