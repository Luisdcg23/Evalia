using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Rúbrica escrita de las dos firmas del informe: la del técnico que lo emite y la del coordinador
    /// que lo aprueba. Se guarda el texto tal como lo escribió cada uno porque es lo que se estampa en
    /// cursiva en el PDF oficial; quién firmó de verdad lo siguen diciendo <c>emitido_por</c> y
    /// <c>revisado_por</c>, que salen de la sesión y se imprimen debajo como aclaración.
    /// </summary>
    public partial class AddReportSignatureNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Firma del técnico -------------------------------------------------------------
            // Entra nula para poder rellenar lo ya emitido antes de exigirla: los informes que existen
            // se firmaron de hecho al emitirse, así que su rúbrica es el nombre registrado de quien los
            // emitió. Sin ese relleno la restricción de abajo rechazaría cada fila existente.
            migrationBuilder.AddColumn<string>(
                name: "firma_nombre",
                table: "Evaluacion_Informe",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql(RellenoFirmaInforme);

            // `oldNullable: true` no es decorativo: sin él, EF compara contra una columna que supone ya
            // no nula, no ve cambio de nulabilidad y omite el SET NOT NULL, dejando la columna abierta.
            migrationBuilder.AlterColumn<string>(
                name: "firma_nombre",
                table: "Evaluacion_Informe",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_Informe_Firma",
                table: "Evaluacion_Informe",
                sql: "char_length(firma_nombre) > 0");

            // La rúbrica es parte de lo que se emitió, así que el candado de inmutabilidad tiene que
            // cubrirla: sin esto se podría reescribir la firma de un informe ya comunicado.
            migrationBuilder.Sql(InformeInmutableConFirma);

            // --- Firma del coordinador ---------------------------------------------------------
            // Nula a propósito: solo la aprobación se firma. Devolver o pedir corrección no estampa
            // nada en el documento, y la restricción obliga a que sea exactamente así.
            migrationBuilder.AddColumn<string>(
                name: "firma_nombre",
                table: "Evaluacion_Informe_Revision",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql(RellenoFirmaRevision);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_Informe_Revision_Firma",
                table: "Evaluacion_Informe_Revision",
                sql: "(decision = 'APPROVED' AND char_length(firma_nombre) > 0) OR (decision <> 'APPROVED' AND firma_nombre IS NULL)");

            // El procedimiento gana el parámetro de la rúbrica. Cambia su firma, así que se elimina la
            // versión anterior: CREATE OR REPLACE dejaría las dos y la llamada quedaría ambigua.
            migrationBuilder.Sql("""
                DROP PROCEDURE IF EXISTS sp_revisar_informe(integer, text, text, uuid);
                """);
            migrationBuilder.Sql(RevisarInformeConFirma);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP PROCEDURE IF EXISTS sp_revisar_informe(integer, text, text, uuid, text);
                """);
            migrationBuilder.Sql(RevisarInformeOriginal);
            migrationBuilder.Sql(InformeInmutableOriginal);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_Informe_Revision_Firma",
                table: "Evaluacion_Informe_Revision");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_Informe_Firma",
                table: "Evaluacion_Informe");

            migrationBuilder.DropColumn(
                name: "firma_nombre",
                table: "Evaluacion_Informe_Revision");

            migrationBuilder.DropColumn(
                name: "firma_nombre",
                table: "Evaluacion_Informe");
        }

        /// <summary>
        /// La rúbrica de lo ya emitido es el nombre registrado de quien lo emitió. El segundo UPDATE
        /// cubre el caso sin nombre utilizable, para que ninguna fila quede fuera de la restricción.
        /// </summary>
        private const string RellenoFirmaInforme = """
            UPDATE "Evaluacion_Informe" i
               SET firma_nombre = left(u."FullName", 80)
              FROM "AspNetUsers" u
             WHERE u."Id" = i.emitido_por AND i.firma_nombre IS NULL;

            UPDATE "Evaluacion_Informe"
               SET firma_nombre = 'Tecnico Evaluador'
             WHERE firma_nombre IS NULL OR btrim(firma_nombre) = '';
            """;

        /// <summary>
        /// Igual que el anterior pero solo sobre las aprobaciones, y suspendiendo el trigger que vuelve
        /// inmutable la revisión: sin eso el relleno se rechazaría a sí mismo.
        /// </summary>
        private const string RellenoFirmaRevision = """
            ALTER TABLE "Evaluacion_Informe_Revision"
                DISABLE TRIGGER tr_evaluacion_informe_revision_inmutable;

            UPDATE "Evaluacion_Informe_Revision" r
               SET firma_nombre = left(u."FullName", 80)
              FROM "AspNetUsers" u
             WHERE u."Id" = r.revisado_por
               AND r.decision = 'APPROVED'
               AND r.firma_nombre IS NULL;

            UPDATE "Evaluacion_Informe_Revision"
               SET firma_nombre = 'Coordinador EBR'
             WHERE decision = 'APPROVED' AND (firma_nombre IS NULL OR btrim(firma_nombre) = '');

            UPDATE "Evaluacion_Informe_Revision"
               SET firma_nombre = NULL
             WHERE decision <> 'APPROVED' AND firma_nombre IS NOT NULL;

            ALTER TABLE "Evaluacion_Informe_Revision"
                ENABLE TRIGGER tr_evaluacion_informe_revision_inmutable;
            """;

        private const string InformeInmutableConFirma = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_informe_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Una versión emitida del informe no se elimina.';
                END IF;
                IF NEW.instancia_id IS DISTINCT FROM OLD.instancia_id
                    OR NEW.version IS DISTINCT FROM OLD.version
                    OR NEW.resumen_ejecutivo IS DISTINCT FROM OLD.resumen_ejecutivo
                    OR NEW.hallazgos IS DISTINCT FROM OLD.hallazgos
                    OR NEW.recomendaciones IS DISTINCT FROM OLD.recomendaciones
                    OR NEW.firma_nombre IS DISTINCT FROM OLD.firma_nombre
                    OR NEW.fecha_emision IS DISTINCT FROM OLD.fecha_emision
                    OR NEW.emitido_por IS DISTINCT FROM OLD.emitido_por THEN
                    RAISE EXCEPTION 'El contenido de una versión emitida del informe no admite modificación; una corrección se emite como versión nueva.';
                END IF;
                RETURN NEW;
            END;
            $funcion$;
            """;

        private const string InformeInmutableOriginal = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_informe_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Una versión emitida del informe no se elimina.';
                END IF;
                IF NEW.instancia_id IS DISTINCT FROM OLD.instancia_id
                    OR NEW.version IS DISTINCT FROM OLD.version
                    OR NEW.resumen_ejecutivo IS DISTINCT FROM OLD.resumen_ejecutivo
                    OR NEW.hallazgos IS DISTINCT FROM OLD.hallazgos
                    OR NEW.recomendaciones IS DISTINCT FROM OLD.recomendaciones
                    OR NEW.fecha_emision IS DISTINCT FROM OLD.fecha_emision
                    OR NEW.emitido_por IS DISTINCT FROM OLD.emitido_por THEN
                    RAISE EXCEPTION 'El contenido de una versión emitida del informe no admite modificación; una corrección se emite como versión nueva.';
                END IF;
                RETURN NEW;
            END;
            $funcion$;
            """;

        private const string RevisarInformeConFirma = """
            CREATE OR REPLACE PROCEDURE sp_revisar_informe(
                IN p_instancia_id integer,
                IN p_decision text,
                IN p_observaciones text,
                IN p_usuario_id uuid,
                IN p_firma_nombre text)
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
                p_firma_nombre := nullif(btrim(coalesce(p_firma_nombre, '')), '');
                IF p_decision NOT IN ('APPROVED', 'RETURNED', 'CORRECTION_REQUESTED') THEN
                    RAISE EXCEPTION 'La decision % no pertenece a la revision del informe.', p_decision;
                END IF;
                IF p_decision <> 'APPROVED' AND p_observaciones = '' THEN
                    RAISE EXCEPTION 'Devolver el informe exige observaciones que indiquen que corregir.';
                END IF;
                IF p_decision = 'APPROVED' AND p_firma_nombre IS NULL THEN
                    RAISE EXCEPTION 'Aprobar el informe exige la firma del coordinador.';
                END IF;
                IF char_length(p_firma_nombre) > 80 THEN
                    RAISE EXCEPTION 'La firma no admite mas de 80 caracteres.';
                END IF;
                IF p_decision <> 'APPROVED' THEN
                    p_firma_nombre := NULL;
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
                    (informe_id, decision, observaciones, firma_nombre, fecha_revision, revisado_por)
                VALUES (v_informe_id, p_decision, p_observaciones, p_firma_nombre, now(), p_usuario_id);
                UPDATE "Caso" SET estado = v_destino WHERE "Id" = v_caso_id;
                INSERT INTO "Caso_Estado_Historial"
                    (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (v_caso_id, 'IN_REVIEW', v_destino,
                    format('Revision del informe version %s: %s', v_version, p_decision), now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_revisar_informe(integer, text, text, uuid, text) FROM PUBLIC;
            """;

        private const string RevisarInformeOriginal = """
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
    }
}
