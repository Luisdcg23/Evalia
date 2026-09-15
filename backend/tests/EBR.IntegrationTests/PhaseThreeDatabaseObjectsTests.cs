using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EBR.IntegrationTests;

/// <summary>
/// Verifica que las migraciones de la fase 3 (asignación, agenda y evaluación) instalen por sí
/// mismas todos los objetos de PostgreSQL del ciclo y que revertirlas deje la base coherente con el
/// esquema anterior, sin procedimientos huérfanos que apunten a tablas ya eliminadas.
/// </summary>
public sealed class PhaseThreeDatabaseObjectsTests
{
    private const string BeforePhaseThree = "20260909214906_AddInstitutionalScheduling";
    private const string LastPhaseThree = "20260909223458_AddEvaluationInstanceAndResponses";

    [Fact]
    public void PhaseThreeMigrationsCreateEveryProcedureAndTrigger()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript(BeforePhaseThree, LastPhaseThree);

        Assert.Contains("CREATE OR REPLACE PROCEDURE sp_asignar_tecnico", sql);
        Assert.Contains("CREATE OR REPLACE PROCEDURE sp_programar_evaluacion", sql);
        Assert.Contains("CREATE OR REPLACE PROCEDURE sp_iniciar_evaluacion", sql);
        Assert.Contains("CREATE OR REPLACE PROCEDURE sp_guardar_respuestas", sql);
        Assert.Contains("CREATE OR REPLACE PROCEDURE sp_enviar_evaluacion", sql);
        Assert.Contains("tr_caso_asignacion_inmutable", sql);
        Assert.Contains("tr_evaluacion_instancia_inmutable", sql);
        Assert.Contains("tr_evaluacion_respuesta_item_plantilla", sql);
        Assert.Contains("tr_evaluacion_respuesta_bloqueada", sql);
        Assert.Contains("OLD.estado = 'IN_PROGRESS' AND NEW.estado = 'SUBMITTED'", sql);
        Assert.Contains("to_jsonb(OLD) - ARRAY['estado', 'fecha_envio', 'enviado_por']", sql);
        Assert.Contains("REVOKE ALL ON PROCEDURE sp_iniciar_evaluacion", sql);
    }

    [Fact]
    public void ReassigningATechnicianCancelsTheScheduleOfTheCaseBeingReassigned()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript(BeforePhaseThree, LastPhaseThree);

        var conCancelacion = sql.LastIndexOf(
            "CREATE OR REPLACE PROCEDURE sp_asignar_tecnico", StringComparison.Ordinal);
        var cuerpo = sql[conCancelacion..];

        Assert.Contains("Cancelada automáticamente por reasignación de técnico.", cuerpo);
        Assert.Contains("UPDATE \"Caso_Programacion\"", cuerpo);
    }

    [Fact]
    public void RollingBackPhaseThreeRestoresTheAssignmentProcedureWithoutTheScheduleTable()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript(LastPhaseThree, BeforePhaseThree);

        Assert.Contains("DROP TRIGGER IF EXISTS tr_evaluacion_instancia_inmutable", sql);
        Assert.Contains("DROP TRIGGER IF EXISTS tr_evaluacion_respuesta_item_plantilla", sql);
        Assert.Contains("DROP PROCEDURE IF EXISTS sp_programar_evaluacion", sql);
        Assert.Contains("DROP PROCEDURE IF EXISTS sp_asignar_tecnico", sql);
        Assert.DoesNotContain("Cancelada automáticamente por reasignación de técnico.", sql);
    }
}
