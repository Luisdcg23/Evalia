using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>sp_iniciar_evaluacion</c> —el procedimiento que de verdad ejecuta el inicio de una evaluación
    /// contra PostgreSQL, ver <c>EvaluationInstanceEndpoints.StartAsync</c>— tenía su propia copia del
    /// mismo criterio roto que el endpoint: <c>ORDER BY fecha_publicacion DESC, "Id" DESC LIMIT 1</c>
    /// sobre <c>Plantilla_Evaluacion</c>. El endpoint ya se corrigió para resolver la plantilla activa
    /// (<c>Plantilla_Activa</c>, migración <c>AddActiveEvaluationTemplate</c>) antes de llamar a este
    /// procedimiento, pero el procedimiento ignoraba ese resultado y volvía a elegir "la publicada más
    /// recientemente" por su cuenta: contra PostgreSQL real (la única ruta que ejecuta este
    /// procedimiento; las pruebas de integración corren en InMemory y nunca lo invocan) el bug seguía
    /// vivo pese al arreglo del endpoint. Esta migración redefine el procedimiento para que también use
    /// la plantilla activa.
    /// </summary>
    public partial class FixStartEvaluationUsesActiveTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IniciarEvaluacionConPlantillaActiva);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IniciarEvaluacionConVersionDeReglas);
        }

        private const string IniciarEvaluacionConPlantillaActiva = """
            CREATE OR REPLACE PROCEDURE sp_iniciar_evaluacion(
                IN p_caso_id integer,
                IN p_usuario_id uuid,
                IN p_version_regla_id integer)
            LANGUAGE plpgsql AS $procedimiento$
            DECLARE
                v_caso record;
                v_plantilla record;
                v_plantilla_activa_id integer;
            BEGIN
                SELECT "Id", estado INTO v_caso FROM "Caso" WHERE "Id" = p_caso_id FOR UPDATE;
                IF NOT FOUND THEN RAISE EXCEPTION 'El caso % no existe.', p_caso_id; END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM "Caso_Asignacion" a
                    JOIN "AspNetUsers" u ON u."Id" = a.tecnico_id
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE a.caso_id = p_caso_id AND a.vigente AND a.tecnico_id = p_usuario_id
                      AND u."ApprovalStatus" = 1 AND r."Name" = 'TECNICO_EVALUADOR'
                ) THEN
                    RAISE EXCEPTION 'El usuario no es el técnico evaluador vigente y aprobado del caso.';
                END IF;
                IF EXISTS (SELECT 1 FROM "Evaluacion_Instancia" WHERE caso_id = p_caso_id) THEN RETURN; END IF;
                IF v_caso.estado <> 'SCHEDULED' THEN
                    RAISE EXCEPTION 'No se permite iniciar una evaluación desde el estado %.', v_caso.estado;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM "Version_Regla_Riesgo"
                    WHERE "Id" = p_version_regla_id AND activo AND publicado
                      AND vigente_desde <= now() AND (vigente_hasta IS NULL OR vigente_hasta > now())
                ) THEN
                    RAISE EXCEPTION 'No hay una versión publicada de reglas de riesgo con la que calificar la evaluación.';
                END IF;

                SELECT plantilla_id INTO v_plantilla_activa_id FROM "Plantilla_Activa" WHERE "Id" = 1;
                IF v_plantilla_activa_id IS NULL THEN
                    RAISE EXCEPTION 'No hay una plantilla de evaluación activa. Un administrador debe activar una plantilla publicada antes de iniciar evaluaciones.';
                END IF;
                SELECT "Id", familia_id INTO v_plantilla FROM "Plantilla_Evaluacion"
                    WHERE "Id" = v_plantilla_activa_id AND estado = 'PUBLISHED';
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'La plantilla activa ya no está publicada. Un administrador debe activar otra plantilla publicada antes de iniciar evaluaciones.';
                END IF;

                INSERT INTO "Evaluacion_Instancia"
                    (caso_id, plantilla_id, familia_plantilla_id, version_regla_riesgo_id, estado, fecha_inicio, iniciado_por)
                VALUES (p_caso_id, v_plantilla."Id", v_plantilla.familia_id, p_version_regla_id, 'IN_PROGRESS', now(), p_usuario_id);
                UPDATE "Caso" SET estado = 'IN_EVALUATION' WHERE "Id" = p_caso_id;
                INSERT INTO "Caso_Estado_Historial"
                    (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (p_caso_id, 'SCHEDULED', 'IN_EVALUATION', 'Inicio de evaluación', now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_iniciar_evaluacion(integer, uuid, integer) FROM PUBLIC;
            """;

        /// <summary>Versión anterior (criterio roto), reinstalada solo si se revierte esta migración.</summary>
        private const string IniciarEvaluacionConVersionDeReglas = """
            CREATE OR REPLACE PROCEDURE sp_iniciar_evaluacion(
                IN p_caso_id integer,
                IN p_usuario_id uuid,
                IN p_version_regla_id integer)
            LANGUAGE plpgsql AS $procedimiento$
            DECLARE
                v_caso record;
                v_plantilla record;
            BEGIN
                SELECT "Id", estado INTO v_caso FROM "Caso" WHERE "Id" = p_caso_id FOR UPDATE;
                IF NOT FOUND THEN RAISE EXCEPTION 'El caso % no existe.', p_caso_id; END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM "Caso_Asignacion" a
                    JOIN "AspNetUsers" u ON u."Id" = a.tecnico_id
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE a.caso_id = p_caso_id AND a.vigente AND a.tecnico_id = p_usuario_id
                      AND u."ApprovalStatus" = 1 AND r."Name" = 'TECNICO_EVALUADOR'
                ) THEN
                    RAISE EXCEPTION 'El usuario no es el técnico evaluador vigente y aprobado del caso.';
                END IF;
                IF EXISTS (SELECT 1 FROM "Evaluacion_Instancia" WHERE caso_id = p_caso_id) THEN RETURN; END IF;
                IF v_caso.estado <> 'SCHEDULED' THEN
                    RAISE EXCEPTION 'No se permite iniciar una evaluación desde el estado %.', v_caso.estado;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM "Version_Regla_Riesgo"
                    WHERE "Id" = p_version_regla_id AND activo AND publicado
                      AND vigente_desde <= now() AND (vigente_hasta IS NULL OR vigente_hasta > now())
                ) THEN
                    RAISE EXCEPTION 'No hay una versión publicada de reglas de riesgo con la que calificar la evaluación.';
                END IF;
                SELECT "Id", familia_id INTO v_plantilla FROM "Plantilla_Evaluacion"
                    WHERE estado = 'PUBLISHED' ORDER BY fecha_publicacion DESC NULLS LAST, "Id" DESC LIMIT 1;
                IF NOT FOUND THEN RAISE EXCEPTION 'No existe una plantilla de evaluación publicada.'; END IF;
                INSERT INTO "Evaluacion_Instancia"
                    (caso_id, plantilla_id, familia_plantilla_id, version_regla_riesgo_id, estado, fecha_inicio, iniciado_por)
                VALUES (p_caso_id, v_plantilla."Id", v_plantilla.familia_id, p_version_regla_id, 'IN_PROGRESS', now(), p_usuario_id);
                UPDATE "Caso" SET estado = 'IN_EVALUATION' WHERE "Id" = p_caso_id;
                INSERT INTO "Caso_Estado_Historial"
                    (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (p_caso_id, 'SCHEDULED', 'IN_EVALUATION', 'Inicio de evaluación', now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_iniciar_evaluacion(integer, uuid, integer) FROM PUBLIC;
            """;
    }
}
