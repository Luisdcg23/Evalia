using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationInstanceAndResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evaluacion_Instancia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    plantilla_id = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    iniciado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    enviado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_Instancia", x => x.Id);
                    table.CheckConstraint("CK_Evaluacion_Instancia_Estado", "estado IN ('IN_PROGRESS','SUBMITTED')");
                    table.CheckConstraint("CK_Evaluacion_Instancia_Envio", "(estado = 'IN_PROGRESS' AND fecha_envio IS NULL AND enviado_por IS NULL) OR (estado = 'SUBMITTED' AND fecha_envio IS NOT NULL AND enviado_por IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Evaluacion_Instancia_AspNetUsers_enviado_por",
                        column: x => x.enviado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Instancia_AspNetUsers_iniciado_por",
                        column: x => x.iniciado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Instancia_Caso_caso_id",
                        column: x => x.caso_id,
                        principalTable: "Caso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Instancia_Plantilla_Evaluacion_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "Plantilla_Evaluacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Evaluacion_Respuesta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instancia_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    opcion = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: false),
                    comentarios = table.Column<string>(type: "text", nullable: false),
                    fecha_guardado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guardado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_Respuesta", x => x.Id);
                    table.CheckConstraint("CK_Evaluacion_Respuesta_Opcion", "opcion IN ('C','CP','IT','NA')");
                    table.ForeignKey(
                        name: "FK_Evaluacion_Respuesta_AspNetUsers_guardado_por",
                        column: x => x.guardado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Respuesta_Evaluacion_Instancia_instancia_id",
                        column: x => x.instancia_id,
                        principalTable: "Evaluacion_Instancia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Respuesta_Plantilla_Evaluacion_Item_item_id",
                        column: x => x.item_id,
                        principalTable: "Plantilla_Evaluacion_Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Instancia_caso_id",
                table: "Evaluacion_Instancia",
                column: "caso_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Instancia_enviado_por",
                table: "Evaluacion_Instancia",
                column: "enviado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Instancia_iniciado_por",
                table: "Evaluacion_Instancia",
                column: "iniciado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Instancia_plantilla_id",
                table: "Evaluacion_Instancia",
                column: "plantilla_id");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Respuesta_guardado_por",
                table: "Evaluacion_Respuesta",
                column: "guardado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Respuesta_instancia_id_item_id",
                table: "Evaluacion_Respuesta",
                columns: ["instancia_id", "item_id"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Respuesta_item_id",
                table: "Evaluacion_Respuesta",
                column: "item_id");

            migrationBuilder.Sql(InstanciaInmutable);
            migrationBuilder.Sql(RespuestaItemPlantilla);
            migrationBuilder.Sql(RespuestaBloqueada);
            migrationBuilder.Sql(IniciarEvaluacion);
            migrationBuilder.Sql(GuardarRespuestas);
            migrationBuilder.Sql(EnviarEvaluacion);
        }

        /// <summary>
        /// Congela la cabecera de la evaluación: una vez creada, la única modificación admitida es el
        /// envío (<c>IN_PROGRESS</c> a <c>SUBMITTED</c> fijando fecha y autor de envío). Cualquier otro
        /// cambio de columna, y toda eliminación, se rechazan.
        /// </summary>
        private const string InstanciaInmutable = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_instancia_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'La cabecera de una evaluación no admite eliminaciones.';
                END IF;
                IF OLD.estado = 'IN_PROGRESS' AND NEW.estado = 'SUBMITTED' AND
                   OLD.fecha_envio IS NULL AND OLD.enviado_por IS NULL AND
                   NEW.fecha_envio IS NOT NULL AND NEW.enviado_por IS NOT NULL AND
                   (to_jsonb(OLD) - ARRAY['estado', 'fecha_envio', 'enviado_por']) =
                   (to_jsonb(NEW) - ARRAY['estado', 'fecha_envio', 'enviado_por']) THEN
                    RETURN NEW;
                END IF;
                RAISE EXCEPTION 'La cabecera de evaluación solo permite el envío de IN_PROGRESS a SUBMITTED.';
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_instancia_inmutable ON "Evaluacion_Instancia";
            CREATE TRIGGER tr_evaluacion_instancia_inmutable
                BEFORE UPDATE OR DELETE ON "Evaluacion_Instancia"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_instancia_inmutable();
            """;

        /// <summary>
        /// Garantiza que cada respuesta corresponda a una pregunta activa de la plantilla congelada en
        /// la instancia, incluso si la fila se inserta directamente por SQL sin pasar por
        /// <c>sp_guardar_respuestas</c>.
        /// </summary>
        private const string RespuestaItemPlantilla = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_respuesta_item_plantilla() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM "Evaluacion_Instancia" i
                    JOIN "Plantilla_Evaluacion_Item" p ON p."Id" = NEW.item_id
                    WHERE i."Id" = NEW.instancia_id
                      AND p.plantilla_id = i.plantilla_id
                      AND p.activo
                      AND p.tipo_item = 'QUESTION'
                ) THEN
                    RAISE EXCEPTION 'La respuesta no pertenece a una pregunta activa de la plantilla congelada.';
                END IF;
                RETURN NEW;
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_respuesta_item_plantilla ON "Evaluacion_Respuesta";
            CREATE TRIGGER tr_evaluacion_respuesta_item_plantilla
                BEFORE INSERT OR UPDATE ON "Evaluacion_Respuesta"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_respuesta_item_plantilla();
            """;

        /// <summary>
        /// Bloquea las respuestas de una evaluación ya enviada: tras el envío no se admiten altas,
        /// modificaciones ni bajas.
        /// </summary>
        private const string RespuestaBloqueada = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_respuesta_bloqueada() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            DECLARE
                v_estado text;
            BEGIN
                SELECT estado INTO v_estado
                    FROM "Evaluacion_Instancia"
                    WHERE "Id" = COALESCE(NEW.instancia_id, OLD.instancia_id);
                IF v_estado = 'SUBMITTED' THEN
                    RAISE EXCEPTION 'Las respuestas de una evaluación enviada son inmutables.';
                END IF;
                RETURN COALESCE(NEW, OLD);
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_respuesta_bloqueada ON "Evaluacion_Respuesta";
            CREATE TRIGGER tr_evaluacion_respuesta_bloqueada
                BEFORE INSERT OR UPDATE OR DELETE ON "Evaluacion_Respuesta"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_respuesta_bloqueada();
            """;

        /// <summary>
        /// Guarda o actualiza (autosave idempotente) la respuesta de una pregunta. Valida que la
        /// instancia exista y no esté enviada, que el ítem sea una pregunta activa de la plantilla de
        /// la instancia, que la opción pertenezca a esa plantilla y que <c>NA</c> solo se use si la
        /// pregunta lo permite; después inserta o actualiza con <c>ON CONFLICT</c> sobre el índice único
        /// (<c>instancia_id</c>, <c>item_id</c>).
        /// </summary>
        private const string GuardarRespuestas = """
            CREATE OR REPLACE PROCEDURE sp_guardar_respuestas(
                IN p_instancia_id integer,
                IN p_item_id integer,
                IN p_opcion text,
                IN p_observaciones text,
                IN p_comentarios text,
                IN p_usuario_id uuid)
            LANGUAGE plpgsql
            AS $procedimiento$
            DECLARE
                v_instancia record;
                v_item record;
                v_opcion text := upper(btrim(coalesce(p_opcion, '')));
                v_observaciones text := coalesce(btrim(p_observaciones), '');
                v_comentarios text := coalesce(btrim(p_comentarios), '');
            BEGIN
                SELECT "Id", caso_id, plantilla_id, estado INTO v_instancia
                    FROM "Evaluacion_Instancia" WHERE "Id" = p_instancia_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'La instancia de evaluación % no existe.', p_instancia_id;
                END IF;
                IF v_instancia.estado = 'SUBMITTED' THEN
                    RAISE EXCEPTION 'La evaluación % ya fue enviada y sus respuestas están bloqueadas.', p_instancia_id;
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

                SELECT "Id", tipo_item, permite_no_aplica INTO v_item
                    FROM "Plantilla_Evaluacion_Item"
                    WHERE "Id" = p_item_id AND plantilla_id = v_instancia.plantilla_id AND activo;
                IF NOT FOUND OR v_item.tipo_item <> 'QUESTION' THEN
                    RAISE EXCEPTION 'El ítem % no es una pregunta activa de la plantilla de esta evaluación.', p_item_id;
                END IF;

                IF v_opcion = '' OR NOT EXISTS (
                    SELECT 1 FROM "Plantilla_Evaluacion_Opcion"
                    WHERE plantilla_id = v_instancia.plantilla_id AND codigo = v_opcion
                ) THEN
                    RAISE EXCEPTION 'La opción de respuesta % no pertenece a la plantilla de esta evaluación.', p_opcion;
                END IF;
                IF v_opcion = 'NA' AND NOT v_item.permite_no_aplica THEN
                    RAISE EXCEPTION 'La pregunta % no admite la opción No Aplica.', p_item_id;
                END IF;

                INSERT INTO "Evaluacion_Respuesta"
                    (instancia_id, item_id, opcion, observaciones, comentarios, fecha_guardado, guardado_por)
                VALUES (p_instancia_id, p_item_id, v_opcion, v_observaciones, v_comentarios, now(), p_usuario_id)
                ON CONFLICT (instancia_id, item_id) DO UPDATE
                    SET opcion = excluded.opcion,
                        observaciones = excluded.observaciones,
                        comentarios = excluded.comentarios,
                        fecha_guardado = excluded.fecha_guardado,
                        guardado_por = excluded.guardado_por;
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_guardar_respuestas(integer, integer, text, text, text, uuid) FROM PUBLIC;
            """;

        private const string IniciarEvaluacion = """
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

        /// <summary>
        /// Envía (bloquea) una evaluación en curso y transiciona su caso a <c>PENDING_REPORT</c>. En esta
        /// versión no exige que todas las preguntas estén respondidas; la migración
        /// <c>AddEvaluationResult</c> la redefine añadiendo esa exigencia, porque el porcentaje BPM se
        /// calcula sobre la ficha completa.
        /// </summary>
        private const string EnviarEvaluacion = """
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
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_iniciar_evaluacion(integer, uuid);");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_enviar_evaluacion(integer, uuid);");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_guardar_respuestas(integer, integer, text, text, text, uuid);");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_evaluacion_respuesta_bloqueada ON \"Evaluacion_Respuesta\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_evaluacion_respuesta_bloqueada();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_evaluacion_respuesta_item_plantilla ON \"Evaluacion_Respuesta\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_evaluacion_respuesta_item_plantilla();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_evaluacion_instancia_inmutable ON \"Evaluacion_Instancia\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_evaluacion_instancia_inmutable();");

            migrationBuilder.DropTable(
                name: "Evaluacion_Respuesta");

            migrationBuilder.DropTable(
                name: "Evaluacion_Instancia");
        }
    }
}
