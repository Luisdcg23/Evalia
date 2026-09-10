using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Caso_Programacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_programada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    programado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_cancelacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_cancelacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caso_Programacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caso_Programacion_AspNetUsers_cancelado_por",
                        column: x => x.cancelado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Programacion_AspNetUsers_programado_por",
                        column: x => x.programado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Programacion_AspNetUsers_tecnico_id",
                        column: x => x.tecnico_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Programacion_Caso_caso_id",
                        column: x => x.caso_id,
                        principalTable: "Caso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Programacion_cancelado_por",
                table: "Caso_Programacion",
                column: "cancelado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Programacion_caso_id_fecha_creacion",
                table: "Caso_Programacion",
                columns: ["caso_id", "fecha_creacion"]);

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Programacion_caso_id_vigente",
                table: "Caso_Programacion",
                column: "caso_id",
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Programacion_programado_por",
                table: "Caso_Programacion",
                column: "programado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Programacion_tecnico_id_vigente",
                table: "Caso_Programacion",
                columns: ["tecnico_id", "vigente"]);

            migrationBuilder.Sql(ProgramarEvaluacion);
            migrationBuilder.Sql(AsignarTecnicoConCancelacion);
        }

        private const string ProgramarEvaluacion = """
            CREATE OR REPLACE PROCEDURE sp_programar_evaluacion(
                IN p_caso_id integer,
                IN p_accion text,
                IN p_fecha_programada timestamptz,
                IN p_prioridad text,
                IN p_motivo text,
                IN p_observaciones text,
                IN p_usuario_id uuid)
            LANGUAGE plpgsql
            AS $procedimiento$
            DECLARE
                v_caso record;
                v_tecnico_id uuid;
                v_actual record;
                v_motivo text := btrim(coalesce(p_motivo, ''));
                v_prioridad text := btrim(coalesce(p_prioridad, ''));
                v_observaciones text := coalesce(btrim(p_observaciones), '');
                v_fin_nueva timestamptz;
                v_solapa boolean;
            BEGIN
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

                IF p_accion = 'CANCELAR' THEN
                    SELECT * INTO v_actual FROM "Caso_Programacion" WHERE caso_id = p_caso_id AND vigente FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'El caso % no tiene una programación vigente para cancelar.', p_caso_id;
                    END IF;
                    IF v_motivo = '' THEN
                        RAISE EXCEPTION 'El motivo de la cancelación es obligatorio.';
                    END IF;
                    UPDATE "Caso_Programacion"
                        SET vigente = false, fecha_cancelacion = now(), cancelado_por = p_usuario_id, motivo_cancelacion = v_motivo
                        WHERE "Id" = v_actual."Id";
                    IF v_caso.estado = 'SCHEDULED' THEN
                        UPDATE "Caso" SET estado = 'ASSIGNED' WHERE "Id" = p_caso_id;
                        INSERT INTO "Caso_Estado_Historial"
                            (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                        VALUES (p_caso_id, 'SCHEDULED', 'ASSIGNED', v_motivo, now(), p_usuario_id);
                    END IF;
                    RETURN;
                END IF;

                IF v_motivo = '' THEN
                    RAISE EXCEPTION 'El motivo de la programación es obligatorio.';
                END IF;

                SELECT tecnico_id INTO v_tecnico_id FROM "Caso_Asignacion" WHERE caso_id = p_caso_id AND vigente;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'El caso % no tiene técnico asignado.', p_caso_id;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM "AspNetUsers" u
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE u."Id" = v_tecnico_id AND u."ApprovalStatus" = 1 AND r."Name" = 'TECNICO_EVALUADOR'
                ) THEN
                    RAISE EXCEPTION 'El técnico asignado no está aprobado o no tiene el rol Técnico Evaluador.';
                END IF;

                PERFORM pg_advisory_xact_lock(hashtextextended(v_tecnico_id::text, 0));

                IF p_accion = 'PROGRAMAR' THEN
                    IF v_caso.estado <> 'ASSIGNED' THEN
                        RAISE EXCEPTION 'No se permite programar un caso en estado %.', v_caso.estado;
                    END IF;
                ELSIF p_accion = 'REPROGRAMAR' THEN
                    IF v_caso.estado NOT IN
                        ('ASSIGNED', 'SCHEDULED', 'IN_EVALUATION', 'PENDING_REPORT', 'IN_REVIEW', 'CORRECTION_REQUIRED') THEN
                        RAISE EXCEPTION 'No se permite reprogramar un caso en estado %.', v_caso.estado;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "Caso_Programacion" WHERE caso_id = p_caso_id AND vigente) THEN
                        RAISE EXCEPTION 'El caso % no tiene una programación vigente para reprogramar.', p_caso_id;
                    END IF;
                ELSE
                    RAISE EXCEPTION 'Acción % no reconocida.', p_accion;
                END IF;

                v_fin_nueva := p_fecha_programada + interval '2 hours';
                SELECT EXISTS (
                    SELECT 1 FROM "Caso_Programacion"
                    WHERE vigente AND tecnico_id = v_tecnico_id AND caso_id <> p_caso_id
                      AND p_fecha_programada < (fecha_programada + interval '2 hours')
                      AND fecha_programada < v_fin_nueva
                ) INTO v_solapa;
                IF v_solapa THEN
                    RAISE EXCEPTION 'El técnico % ya tiene otra programación vigente que se solapa con ese horario.', v_tecnico_id;
                END IF;

                UPDATE "Caso_Programacion" SET vigente = false WHERE caso_id = p_caso_id AND vigente;

                INSERT INTO "Caso_Programacion"
                    (caso_id, tecnico_id, fecha_programada, prioridad, motivo, observaciones, vigente, fecha_creacion, programado_por)
                VALUES (p_caso_id, v_tecnico_id, p_fecha_programada, v_prioridad, v_motivo, v_observaciones, true, now(), p_usuario_id);

                IF p_accion = 'PROGRAMAR' THEN
                    UPDATE "Caso" SET estado = 'SCHEDULED', prioridad = CASE WHEN v_prioridad = '' THEN prioridad ELSE v_prioridad END
                        WHERE "Id" = p_caso_id;
                    INSERT INTO "Caso_Estado_Historial" (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                    VALUES (p_caso_id, v_caso.estado, 'SCHEDULED', v_motivo, now(), p_usuario_id);
                ELSIF v_prioridad <> '' THEN
                    UPDATE "Caso" SET prioridad = v_prioridad WHERE "Id" = p_caso_id;
                END IF;
            END;
            $procedimiento$;

            REVOKE ALL ON PROCEDURE sp_programar_evaluacion(integer, text, timestamptz, text, text, text, uuid) FROM PUBLIC;
            """;

        /// <summary>
        /// Reemplaza <c>sp_asignar_tecnico</c> para que la reasignación de un caso ya programado
        /// cancele su programación vigente y devuelva el caso a <c>ASSIGNED</c>: la agenda pertenece
        /// al técnico saliente, así que no puede sobrevivir al cambio de técnico. El procedimiento se
        /// redefine aquí y no en la migración que lo creó porque solo ahora existe
        /// <c>Caso_Programacion</c>.
        /// </summary>
        private const string AsignarTecnicoConCancelacion = """
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

                IF v_caso.estado = 'SCHEDULED' THEN
                    UPDATE "Caso_Programacion"
                        SET vigente = false,
                            fecha_cancelacion = now(),
                            cancelado_por = p_usuario_id,
                            motivo_cancelacion = 'Cancelada automáticamente por reasignación de técnico.'
                        WHERE caso_id = p_caso_id AND vigente;
                    UPDATE "Caso" SET estado = 'ASSIGNED' WHERE "Id" = p_caso_id;
                    INSERT INTO "Caso_Estado_Historial" (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                    VALUES (p_caso_id, 'SCHEDULED', 'ASSIGNED', v_motivo, now(), p_usuario_id);
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

        /// <summary>
        /// Versión de <c>sp_asignar_tecnico</c> anterior a la agenda, sin referencias a
        /// <c>Caso_Programacion</c>. Solo se usa en <see cref="Down"/> para dejar el procedimiento
        /// coherente con el esquema al que se revierte.
        /// </summary>
        private const string AsignarTecnicoSinCancelacion = """
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP PROCEDURE IF EXISTS sp_programar_evaluacion(integer, text, timestamptz, text, text, text, uuid);");
            migrationBuilder.Sql(AsignarTecnicoSinCancelacion);

            migrationBuilder.DropTable(
                name: "Caso_Programacion");
        }
    }
}
