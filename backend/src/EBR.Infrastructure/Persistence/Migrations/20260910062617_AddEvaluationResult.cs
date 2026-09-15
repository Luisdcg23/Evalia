using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Resultado de la evaluación BPM y su integración con el cálculo de riesgo (RF-14). Añade a la
    /// instancia la familia de plantilla y la versión de reglas de riesgo con las que se calificará
    /// —congeladas al iniciar la evaluación—, crea la fotografía inmutable del resultado con sus no
    /// conformidades y actualiza las rutinas de ejecución: <c>sp_iniciar_evaluacion</c> recibe ahora la
    /// versión de reglas y <c>sp_enviar_evaluacion</c> exige la ficha completa antes de bloquearla.
    /// </summary>
    public partial class AddEvaluationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "familia_plantilla_id",
                table: "Evaluacion_Instancia",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "version_regla_riesgo_id",
                table: "Evaluacion_Instancia",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Evaluacion_Resultado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instancia_id = table.Column<int>(type: "integer", nullable: false),
                    puntos_bpm = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    denominador_bpm = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    porcentaje_bpm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    codigo_calificacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    clasificacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    puntaje_riesgo_bpm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    cantidad_criticas = table.Column<int>(type: "integer", nullable: false),
                    cantidad_mayores = table.Column<int>(type: "integer", nullable: false),
                    cantidad_menores = table.Column<int>(type: "integer", nullable: false),
                    calculo_riesgo_id = table.Column<int>(type: "integer", nullable: false),
                    frecuencia_meses = table.Column<int>(type: "integer", nullable: false),
                    fecha_calculo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_Resultado", x => x.Id);
                    table.CheckConstraint("CK_Evaluacion_Resultado_Conteos", "cantidad_criticas >= 0 AND cantidad_mayores >= 0 AND cantidad_menores >= 0");
                    table.CheckConstraint("CK_Evaluacion_Resultado_Denominador", "denominador_bpm > 0 AND puntos_bpm >= 0 AND puntos_bpm <= denominador_bpm");
                    table.CheckConstraint("CK_Evaluacion_Resultado_Frecuencia", "frecuencia_meses > 0");
                    table.CheckConstraint("CK_Evaluacion_Resultado_Porcentaje", "porcentaje_bpm BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_Evaluacion_Resultado_Calculo_Riesgo_calculo_riesgo_id",
                        column: x => x.calculo_riesgo_id,
                        principalTable: "Calculo_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Resultado_Evaluacion_Instancia_instancia_id",
                        column: x => x.instancia_id,
                        principalTable: "Evaluacion_Instancia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Evaluacion_No_Conformidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resultado_id = table.Column<int>(type: "integer", nullable: false),
                    respuesta_id = table.Column<int>(type: "integer", nullable: false),
                    criterio_id = table.Column<int>(type: "integer", nullable: false),
                    severidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_deteccion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_No_Conformidad", x => x.Id);
                    table.CheckConstraint("CK_Evaluacion_No_Conformidad_Severidad", "severidad IN ('CRITICAL','MAJOR','MINOR')");
                    table.ForeignKey(
                        name: "FK_Evaluacion_No_Conformidad_Evaluacion_Respuesta_respuesta_id",
                        column: x => x.respuesta_id,
                        principalTable: "Evaluacion_Respuesta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_No_Conformidad_Evaluacion_Resultado_resultado_id",
                        column: x => x.resultado_id,
                        principalTable: "Evaluacion_Resultado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_No_Conformidad_Plantilla_Evaluacion_Criterio_cri~",
                        column: x => x.criterio_id,
                        principalTable: "Plantilla_Evaluacion_Criterio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Instancia_version_regla_riesgo_id",
                table: "Evaluacion_Instancia",
                column: "version_regla_riesgo_id");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_No_Conformidad_criterio_id",
                table: "Evaluacion_No_Conformidad",
                column: "criterio_id");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_No_Conformidad_respuesta_id",
                table: "Evaluacion_No_Conformidad",
                column: "respuesta_id");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_No_Conformidad_resultado_id_criterio_id",
                table: "Evaluacion_No_Conformidad",
                columns: ["resultado_id", "criterio_id"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Resultado_calculo_riesgo_id",
                table: "Evaluacion_Resultado",
                column: "calculo_riesgo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Resultado_instancia_id",
                table: "Evaluacion_Resultado",
                column: "instancia_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Evaluacion_Instancia_Version_Regla_Riesgo_version_regla_rie~",
                table: "Evaluacion_Instancia",
                column: "version_regla_riesgo_id",
                principalTable: "Version_Regla_Riesgo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Los valores por defecto solo existen para poder añadir las columnas como NOT NULL; una
            // instancia nueva siempre declara su familia de plantilla y su versión de reglas, así que
            // se retiran para que una inserción incompleta falle en lugar de guardar ceros.
            migrationBuilder.Sql("""
                ALTER TABLE "Evaluacion_Instancia" ALTER COLUMN familia_plantilla_id DROP DEFAULT;
                ALTER TABLE "Evaluacion_Instancia" ALTER COLUMN version_regla_riesgo_id DROP DEFAULT;
                """);

            migrationBuilder.Sql(ResultadoInmutable);
            migrationBuilder.Sql(IniciarEvaluacionConVersionDeReglas);
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_iniciar_evaluacion(integer, uuid);");
            migrationBuilder.Sql(EnviarEvaluacionCompleta);
        }

        /// <summary>
        /// El resultado de una evaluación es la fotografía con la que se emitirá el informe oficial: no
        /// admite modificación ni eliminación, ni en la cabecera ni en sus no conformidades. Recalcular
        /// exige una evaluación nueva. La misma invariante vive en <c>EbrDbContext</c>, porque las
        /// pruebas de integración corren sobre un proveedor en memoria que no ejecuta disparadores.
        /// </summary>
        private const string ResultadoInmutable = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_resultado_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                RAISE EXCEPTION 'El resultado de una evaluación es una fotografía inmutable y no admite modificación ni eliminación.';
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_resultado_inmutable ON "Evaluacion_Resultado";
            CREATE TRIGGER tr_evaluacion_resultado_inmutable
                BEFORE UPDATE OR DELETE ON "Evaluacion_Resultado"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_resultado_inmutable();

            DROP TRIGGER IF EXISTS tr_evaluacion_resultado_inmutable ON "Evaluacion_No_Conformidad";
            CREATE TRIGGER tr_evaluacion_resultado_inmutable
                BEFORE UPDATE OR DELETE ON "Evaluacion_No_Conformidad"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_resultado_inmutable();
            """;

        /// <summary>
        /// Redefine <c>sp_iniciar_evaluacion</c> con la versión de reglas de riesgo como parámetro: la
        /// instancia congela la familia de la plantilla publicada elegida y la versión de reglas con la
        /// que se calificará, para que publicar otra plantilla u otras reglas durante la inspección no
        /// cambie el resultado de una evaluación ya iniciada. La versión recibida debe estar publicada y
        /// vigente; si no lo está, el inicio se rechaza en lugar de arrastrar reglas en borrador.
        /// </summary>
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

        /// <summary>
        /// Redefine <c>sp_enviar_evaluacion</c> exigiendo la ficha completa. El porcentaje BPM se define
        /// sobre la ficha entera: una pregunta sin responder no equivale a un <c>NA</c> —que sí se
        /// declara y sale del denominador—, así que admitir el envío incompleto haría que el porcentaje
        /// dependiera de cuánto se capturó y no de lo observado. La misma regla vive en
        /// <c>EvaluationSubmissionCalculator</c>.
        /// </summary>
        private const string EnviarEvaluacionCompleta = """
            CREATE OR REPLACE PROCEDURE sp_enviar_evaluacion(
                IN p_instancia_id integer,
                IN p_usuario_id uuid)
            LANGUAGE plpgsql
            AS $procedimiento$
            DECLARE
                v_instancia record;
                v_caso record;
                v_faltantes text;
            BEGIN
                SELECT "Id", caso_id, plantilla_id, estado INTO v_instancia
                    FROM "Evaluacion_Instancia" WHERE "Id" = p_instancia_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'La instancia de evaluación % no existe.', p_instancia_id;
                END IF;
                IF v_instancia.estado = 'SUBMITTED' THEN
                    RAISE EXCEPTION 'La evaluación % ya fue enviada.', p_instancia_id;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM "Caso_Asignacion" a
                    JOIN "AspNetUsers" u ON u."Id" = a.tecnico_id
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE a.caso_id = v_instancia.caso_id AND a.vigente AND a.tecnico_id = p_usuario_id
                      AND u."ApprovalStatus" = 1 AND r."Name" = 'TECNICO_EVALUADOR'
                ) THEN
                    RAISE EXCEPTION 'El usuario no es el técnico evaluador vigente y aprobado del caso.';
                END IF;

                SELECT string_agg(p.codigo, ', ' ORDER BY p.orden, p."Id") INTO v_faltantes
                    FROM "Plantilla_Evaluacion_Item" p
                    WHERE p.plantilla_id = v_instancia.plantilla_id AND p.activo AND p.tipo_item = 'QUESTION'
                      AND NOT EXISTS (
                          SELECT 1 FROM "Evaluacion_Respuesta" r
                          WHERE r.instancia_id = p_instancia_id AND r.item_id = p."Id");
                IF v_faltantes IS NOT NULL THEN
                    RAISE EXCEPTION 'Faltan respuestas para las preguntas: %.', v_faltantes;
                END IF;

                SELECT "Id", estado INTO v_caso FROM "Caso" WHERE "Id" = v_instancia.caso_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'El caso % no existe.', v_instancia.caso_id;
                END IF;
                IF v_caso.estado <> 'IN_EVALUATION' THEN
                    RAISE EXCEPTION 'No se permite enviar la evaluación desde el estado % del caso.', v_caso.estado;
                END IF;

                UPDATE "Evaluacion_Instancia"
                    SET estado = 'SUBMITTED', fecha_envio = now(), enviado_por = p_usuario_id
                    WHERE "Id" = p_instancia_id;

                UPDATE "Caso" SET estado = 'PENDING_REPORT' WHERE "Id" = v_instancia.caso_id;
                INSERT INTO "Caso_Estado_Historial" (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (v_instancia.caso_id, v_caso.estado, 'PENDING_REPORT', 'Envío de evaluación', now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_enviar_evaluacion(integer, uuid) FROM PUBLIC;
            """;

        /// <summary>
        /// Restituye <c>sp_iniciar_evaluacion</c> sin versión de reglas y <c>sp_enviar_evaluacion</c> sin
        /// la exigencia de ficha completa, tal como los dejó la migración que los creó.
        /// </summary>
        private const string IniciarEvaluacionSinVersionDeReglas = """
            CREATE OR REPLACE PROCEDURE sp_iniciar_evaluacion(IN p_caso_id integer, IN p_usuario_id uuid)
            LANGUAGE plpgsql AS $procedimiento$
            DECLARE
                v_caso record;
                v_plantilla_id integer;
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
                SELECT "Id" INTO v_plantilla_id FROM "Plantilla_Evaluacion"
                    WHERE estado = 'PUBLISHED' ORDER BY fecha_publicacion DESC NULLS LAST, "Id" DESC LIMIT 1;
                IF v_plantilla_id IS NULL THEN RAISE EXCEPTION 'No existe una plantilla de evaluación publicada.'; END IF;
                INSERT INTO "Evaluacion_Instancia"
                    (caso_id, plantilla_id, estado, fecha_inicio, iniciado_por)
                VALUES (p_caso_id, v_plantilla_id, 'IN_PROGRESS', now(), p_usuario_id);
                UPDATE "Caso" SET estado = 'IN_EVALUATION' WHERE "Id" = p_caso_id;
                INSERT INTO "Caso_Estado_Historial"
                    (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (p_caso_id, 'SCHEDULED', 'IN_EVALUATION', 'Inicio de evaluación', now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_iniciar_evaluacion(integer, uuid) FROM PUBLIC;
            """;

        private const string EnviarEvaluacionSinCompletitud = """
            CREATE OR REPLACE PROCEDURE sp_enviar_evaluacion(
                IN p_instancia_id integer,
                IN p_usuario_id uuid)
            LANGUAGE plpgsql
            AS $procedimiento$
            DECLARE
                v_instancia record;
                v_caso record;
            BEGIN
                SELECT "Id", caso_id, estado INTO v_instancia
                    FROM "Evaluacion_Instancia" WHERE "Id" = p_instancia_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'La instancia de evaluación % no existe.', p_instancia_id;
                END IF;
                IF v_instancia.estado = 'SUBMITTED' THEN
                    RAISE EXCEPTION 'La evaluación % ya fue enviada.', p_instancia_id;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM "Caso_Asignacion" a
                    JOIN "AspNetUsers" u ON u."Id" = a.tecnico_id
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE a.caso_id = v_instancia.caso_id AND a.vigente AND a.tecnico_id = p_usuario_id
                      AND u."ApprovalStatus" = 1 AND r."Name" = 'TECNICO_EVALUADOR'
                ) THEN
                    RAISE EXCEPTION 'El usuario no es el técnico evaluador vigente y aprobado del caso.';
                END IF;

                SELECT "Id", estado INTO v_caso FROM "Caso" WHERE "Id" = v_instancia.caso_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'El caso % no existe.', v_instancia.caso_id;
                END IF;
                IF v_caso.estado <> 'IN_EVALUATION' THEN
                    RAISE EXCEPTION 'No se permite enviar la evaluación desde el estado % del caso.', v_caso.estado;
                END IF;

                UPDATE "Evaluacion_Instancia"
                    SET estado = 'SUBMITTED', fecha_envio = now(), enviado_por = p_usuario_id
                    WHERE "Id" = p_instancia_id;

                UPDATE "Caso" SET estado = 'PENDING_REPORT' WHERE "Id" = v_instancia.caso_id;
                INSERT INTO "Caso_Estado_Historial" (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (v_instancia.caso_id, v_caso.estado, 'PENDING_REPORT', 'Envío de evaluación', now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_enviar_evaluacion(integer, uuid) FROM PUBLIC;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(EnviarEvaluacionSinCompletitud);
            migrationBuilder.Sql(IniciarEvaluacionSinVersionDeReglas);
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_iniciar_evaluacion(integer, uuid, integer);");
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_evaluacion_resultado_inmutable ON "Evaluacion_No_Conformidad";
                DROP TRIGGER IF EXISTS tr_evaluacion_resultado_inmutable ON "Evaluacion_Resultado";
                DROP FUNCTION IF EXISTS fn_evaluacion_resultado_inmutable();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Evaluacion_Instancia_Version_Regla_Riesgo_version_regla_rie~",
                table: "Evaluacion_Instancia");

            migrationBuilder.DropTable(
                name: "Evaluacion_No_Conformidad");

            migrationBuilder.DropTable(
                name: "Evaluacion_Resultado");

            migrationBuilder.DropIndex(
                name: "IX_Evaluacion_Instancia_version_regla_riesgo_id",
                table: "Evaluacion_Instancia");

            migrationBuilder.DropColumn(
                name: "familia_plantilla_id",
                table: "Evaluacion_Instancia");

            migrationBuilder.DropColumn(
                name: "version_regla_riesgo_id",
                table: "Evaluacion_Instancia");
        }
    }
}
