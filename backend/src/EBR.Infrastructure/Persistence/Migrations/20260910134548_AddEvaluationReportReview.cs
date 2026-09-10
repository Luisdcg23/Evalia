using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Revisión del informe por el coordinador y gestión de correcciones (fase 5.2, RF-17 y RF-18). Se
    /// guarda una fila por decisión y ninguna se reemplaza: las observaciones son lo que el técnico
    /// consulta para corregir, así que forman parte del expediente igual que el informe. La restricción
    /// de comprobación fija el catálogo de decisiones y los disparadores replican en la base las reglas
    /// del endpoint: solo se revisa la última versión emitida y una devolución sin observaciones no es
    /// una devolución.
    /// </summary>
    public partial class AddEvaluationReportReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evaluacion_Informe_Revision",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    informe_id = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: false),
                    fecha_revision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revisado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_Informe_Revision", x => x.Id);
                    table.CheckConstraint(
                        "CK_Evaluacion_Informe_Revision_Decision",
                        "decision IN ('APPROVED', 'RETURNED', 'CORRECTION_REQUESTED')");
                    table.ForeignKey(
                        name: "FK_Evaluacion_Informe_Revision_AspNetUsers_revisado_por",
                        column: x => x.revisado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Informe_Revision_Evaluacion_Informe_informe_id",
                        column: x => x.informe_id,
                        principalTable: "Evaluacion_Informe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Informe_Revision_informe_id",
                table: "Evaluacion_Informe_Revision",
                column: "informe_id");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Informe_Revision_revisado_por",
                table: "Evaluacion_Informe_Revision",
                column: "revisado_por");

            migrationBuilder.Sql(RevisionValida);
            migrationBuilder.Sql(RevisionInmutable);
            migrationBuilder.Sql(RevisarInforme);
        }

        /// <summary>
        /// La revisión se pronuncia sobre la última versión emitida: revisar una versión anterior
        /// dejaría el informe vigente sin decisión y el expediente en un estado que no corresponde a lo
        /// último que se emitió. Y devolver el informe sin decir qué corregir deja al técnico sin nada
        /// que hacer (RF-18), así que las observaciones son obligatorias salvo en la aprobación.
        /// </summary>
        private const string RevisionValida = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_informe_revision_valida() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            DECLARE
                v_instancia integer;
                v_version integer;
                v_ultima integer;
            BEGIN
                SELECT instancia_id, version INTO v_instancia, v_version
                    FROM "Evaluacion_Informe" WHERE "Id" = NEW.informe_id;
                IF v_instancia IS NULL THEN
                    RAISE EXCEPTION 'El informe % no existe.', NEW.informe_id;
                END IF;
                SELECT MAX(version) INTO v_ultima
                    FROM "Evaluacion_Informe" WHERE instancia_id = v_instancia;
                IF v_version <> v_ultima THEN
                    RAISE EXCEPTION 'Solo se revisa la ultima version emitida del informe: se intentó revisar la % y la vigente es la %.',
                        v_version, v_ultima;
                END IF;
                IF NEW.decision <> 'APPROVED' AND btrim(NEW.observaciones) = '' THEN
                    RAISE EXCEPTION 'Devolver el informe exige observaciones que indiquen qué corregir.';
                END IF;
                RETURN NEW;
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_informe_revision_valida ON "Evaluacion_Informe_Revision";
            CREATE TRIGGER tr_evaluacion_informe_revision_valida
                BEFORE INSERT ON "Evaluacion_Informe_Revision"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_informe_revision_valida();
            """;

        /// <summary>
        /// La observación que motivó una corrección es el sustento de la versión siguiente del informe:
        /// reescribirla después dejaría sin explicación el cambio que ya se hizo.
        /// </summary>
        private const string RevisionInmutable = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_informe_revision_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                RAISE EXCEPTION 'Una revisión registrada del informe no admite modificación ni eliminación.';
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_informe_revision_inmutable ON "Evaluacion_Informe_Revision";
            CREATE TRIGGER tr_evaluacion_informe_revision_inmutable
                BEFORE UPDATE OR DELETE ON "Evaluacion_Informe_Revision"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_informe_revision_inmutable();
            """;

        private const string RevisarInforme = """
            CREATE OR REPLACE PROCEDURE sp_revisar_informe(
                IN p_instancia_id integer,
                IN p_decision text,
                IN p_observaciones text,
                IN p_usuario_id uuid)
            LANGUAGE plpgsql AS $procedimiento$
            DECLARE
                v_informe_id integer;
                v_version integer;
                v_caso_id integer;
                v_estado text;
                v_destino text;
            BEGIN
                p_decision := upper(btrim(p_decision));
                p_observaciones := coalesce(btrim(p_observaciones), '');
                IF p_decision NOT IN ('APPROVED', 'RETURNED', 'CORRECTION_REQUESTED') THEN
                    RAISE EXCEPTION 'La decision % no pertenece a la revision del informe.', p_decision;
                END IF;
                IF p_decision <> 'APPROVED' AND p_observaciones = '' THEN
                    RAISE EXCEPTION 'Devolver el informe exige observaciones que indiquen que corregir.';
                END IF;

                SELECT i."Id", i.version, e.caso_id
                  INTO v_informe_id, v_version, v_caso_id
                  FROM "Evaluacion_Informe" i
                  JOIN "Evaluacion_Instancia" e ON e."Id" = i.instancia_id
                 WHERE i.instancia_id = p_instancia_id
                 ORDER BY i.version DESC
                 LIMIT 1;
                IF v_informe_id IS NULL THEN RAISE EXCEPTION 'La evaluacion no tiene informe.'; END IF;

                SELECT estado INTO v_estado FROM "Caso" WHERE "Id" = v_caso_id FOR UPDATE;
                IF v_estado <> 'IN_REVIEW' THEN
                    RAISE EXCEPTION 'El expediente en estado % no esta en revision.', v_estado;
                END IF;

                v_destino := CASE WHEN p_decision = 'APPROVED' THEN 'APPROVED' ELSE 'CORRECTION_REQUIRED' END;
                UPDATE "Evaluacion_Informe" SET estado = p_decision WHERE "Id" = v_informe_id;
                INSERT INTO "Evaluacion_Informe_Revision"
                    (informe_id, decision, observaciones, fecha_revision, revisado_por)
                VALUES (v_informe_id, p_decision, p_observaciones, now(), p_usuario_id);
                UPDATE "Caso" SET estado = v_destino WHERE "Id" = v_caso_id;
                INSERT INTO "Caso_Estado_Historial"
                    (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (v_caso_id, 'IN_REVIEW', v_destino,
                    format('Revision del informe version %s: %s', v_version, p_decision), now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_revisar_informe(integer, text, text, uuid) FROM PUBLIC;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_evaluacion_informe_revision_inmutable ON "Evaluacion_Informe_Revision";
                DROP TRIGGER IF EXISTS tr_evaluacion_informe_revision_valida ON "Evaluacion_Informe_Revision";
                DROP FUNCTION IF EXISTS fn_evaluacion_informe_revision_inmutable();
                DROP FUNCTION IF EXISTS fn_evaluacion_informe_revision_valida();
                DROP PROCEDURE IF EXISTS sp_revisar_informe(integer, text, text, uuid);
                """);

            migrationBuilder.DropTable(
                name: "Evaluacion_Informe_Revision");
        }
    }
}
