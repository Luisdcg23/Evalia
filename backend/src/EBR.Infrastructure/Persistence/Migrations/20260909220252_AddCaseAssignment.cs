using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Caso_Asignacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_asignacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    asignado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caso_Asignacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caso_Asignacion_AspNetUsers_asignado_por",
                        column: x => x.asignado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Asignacion_AspNetUsers_tecnico_id",
                        column: x => x.tecnico_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Asignacion_Caso_caso_id",
                        column: x => x.caso_id,
                        principalTable: "Caso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Asignacion_asignado_por",
                table: "Caso_Asignacion",
                column: "asignado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Asignacion_caso_id_fecha_asignacion",
                table: "Caso_Asignacion",
                columns: ["caso_id", "fecha_asignacion"]);

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Asignacion_caso_id_vigente",
                table: "Caso_Asignacion",
                column: "caso_id",
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Asignacion_tecnico_id",
                table: "Caso_Asignacion",
                column: "tecnico_id");

            migrationBuilder.Sql(AsignarTecnico);
            migrationBuilder.Sql(HistorialAsignacionInmutable);
        }

        private const string AsignarTecnico = """
            CREATE OR REPLACE PROCEDURE sp_asignar_tecnico(
                IN p_caso_id integer,
                IN p_tecnico_id uuid,
                IN p_usuario_id uuid,
                IN p_motivo text)
            LANGUAGE plpgsql
            AS $procedimiento$
            DECLARE
                v_caso record;
                v_aprobado integer;
                v_es_tecnico boolean;
                v_primera_asignacion boolean;
                v_motivo text := btrim(coalesce(p_motivo, ''));
            BEGIN
                IF v_motivo = '' THEN
                    RAISE EXCEPTION 'El motivo de la asignación es obligatorio.';
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM "AspNetUsers" u
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE u."Id" = p_usuario_id AND u."ApprovalStatus" = 1 AND r."Name" = 'COORDINADOR'
                ) THEN
                    RAISE EXCEPTION 'El usuario ejecutor no es un coordinador aprobado.';
                END IF;

                SELECT "Id", estado INTO v_caso FROM "Caso" WHERE "Id" = p_caso_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'El caso % no existe.', p_caso_id;
                END IF;

                SELECT "ApprovalStatus" INTO v_aprobado FROM "AspNetUsers" WHERE "Id" = p_tecnico_id;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'El técnico % no existe.', p_tecnico_id;
                END IF;
                IF v_aprobado <> 1 THEN
                    RAISE EXCEPTION 'El técnico % no está activo (aprobado).', p_tecnico_id;
                END IF;

                SELECT EXISTS (
                    SELECT 1 FROM "AspNetUserRoles" ur
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE ur."UserId" = p_tecnico_id AND r."Name" = 'TECNICO_EVALUADOR'
                ) INTO v_es_tecnico;
                IF NOT v_es_tecnico THEN
                    RAISE EXCEPTION 'El usuario % no tiene el rol Técnico Evaluador.', p_tecnico_id;
                END IF;


                IF EXISTS (SELECT 1 FROM "Caso_Asignacion"
                           WHERE caso_id = p_caso_id AND vigente AND tecnico_id = p_tecnico_id) THEN
                    RETURN;
                END IF;

                v_primera_asignacion := v_caso.estado = 'PENDING_ASSIGNMENT';
                IF NOT v_primera_asignacion AND v_caso.estado NOT IN
                    ('ASSIGNED', 'SCHEDULED', 'IN_EVALUATION', 'PENDING_REPORT', 'IN_REVIEW', 'CORRECTION_REQUIRED') THEN
                    RAISE EXCEPTION 'No se permite asignar ni reasignar un caso en estado %.', v_caso.estado;
                END IF;

                UPDATE "Caso_Asignacion" SET vigente = false WHERE caso_id = p_caso_id AND vigente;

                INSERT INTO "Caso_Asignacion" (caso_id, tecnico_id, vigente, fecha_asignacion, asignado_por, motivo)
                VALUES (p_caso_id, p_tecnico_id, true, now(), p_usuario_id, v_motivo);

                IF v_primera_asignacion THEN
                    UPDATE "Caso" SET estado = 'ASSIGNED' WHERE "Id" = p_caso_id;
                    INSERT INTO "Caso_Estado_Historial" (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                    VALUES (p_caso_id, 'PENDING_ASSIGNMENT', 'ASSIGNED', v_motivo, now(), p_usuario_id);
                END IF;
            END;
            $procedimiento$;

            REVOKE ALL ON PROCEDURE sp_asignar_tecnico(integer, uuid, uuid, text) FROM PUBLIC;
            """;

        private const string HistorialAsignacionInmutable = """
            CREATE OR REPLACE FUNCTION fn_caso_asignacion_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'El historial de asignaciones no admite eliminaciones.';
                END IF;
                IF OLD.vigente AND NOT NEW.vigente AND
                   NEW.caso_id = OLD.caso_id AND NEW.tecnico_id = OLD.tecnico_id AND
                   NEW.fecha_asignacion = OLD.fecha_asignacion AND NEW.asignado_por = OLD.asignado_por AND
                   NEW.motivo = OLD.motivo THEN
                    RETURN NEW;
                END IF;
                RAISE EXCEPTION 'El historial de asignaciones solo permite retirar la vigencia.';
            END;
            $funcion$;
            CREATE TRIGGER tr_caso_asignacion_inmutable
                BEFORE UPDATE OR DELETE ON "Caso_Asignacion"
                FOR EACH ROW EXECUTE FUNCTION fn_caso_asignacion_inmutable();
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_caso_asignacion_inmutable ON \"Caso_Asignacion\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_caso_asignacion_inmutable();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_asignar_tecnico(integer, uuid, uuid, text);");

            migrationBuilder.DropTable(
                name: "Caso_Asignacion");
        }
    }
}
