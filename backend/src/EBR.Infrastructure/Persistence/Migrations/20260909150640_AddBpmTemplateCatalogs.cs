using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmTemplateCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "Plantilla_Evaluacion_Item",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.CreateTable(
                name: "Plantilla_Evaluacion_Criterio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    criticidad = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    hoja_origen = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    celda_origen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Evaluacion_Criterio", x => x.Id);
                    table.CheckConstraint("CK_Criterio_Criticidad", "criticidad IS NULL OR criticidad IN ('CRITICAL','MAJOR','MINOR')");
                    table.ForeignKey(
                        name: "FK_Plantilla_Evaluacion_Criterio_Plantilla_Evaluacion_Item_ite~",
                        column: x => x.item_id,
                        principalTable: "Plantilla_Evaluacion_Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Plantilla_Evaluacion_Opcion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantilla_id = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    cuenta_denominador = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Evaluacion_Opcion", x => x.Id);
                    table.CheckConstraint("CK_Opcion_Evaluable", "(valor IS NULL AND NOT cuenta_denominador) OR (valor IS NOT NULL AND cuenta_denominador AND valor BETWEEN 0 AND 1)");
                    table.ForeignKey(
                        name: "FK_Plantilla_Evaluacion_Opcion_Plantilla_Evaluacion_plantilla_~",
                        column: x => x.plantilla_id,
                        principalTable: "Plantilla_Evaluacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Plantilla_Evaluacion_Regla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantilla_id = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    clasificacion = table.Column<string>(type: "text", nullable: false),
                    accion = table.Column<string>(type: "text", nullable: false),
                    porcentaje_min = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    minimo_incluido = table.Column<bool>(type: "boolean", nullable: false),
                    porcentaje_max = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    maximo_incluido = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Evaluacion_Regla", x => x.Id);
                    table.CheckConstraint("CK_Regla_Calificacion", "porcentaje_min IS NOT NULL OR porcentaje_max IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Plantilla_Evaluacion_Regla_Plantilla_Evaluacion_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "Plantilla_Evaluacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Plantilla_Importacion_Lote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    hoja = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    hash_contenido = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    plantilla_id = table.Column<int>(type: "integer", nullable: true),
                    detalle_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Importacion_Lote", x => x.Id);
                    table.CheckConstraint("CK_Lote_Estado", "estado IN ('PENDIENTE','PROMOVIDO','RECHAZADO')");
                    table.ForeignKey(
                        name: "FK_Plantilla_Importacion_Lote_Plantilla_Evaluacion_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "Plantilla_Evaluacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Plantilla_Importacion_Fila",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_id = table.Column<int>(type: "integer", nullable: false),
                    celda_origen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    codigo_padre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    tipo_item = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    detalle_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Importacion_Fila", x => x.Id);
                    table.CheckConstraint("CK_Fila_Estado", "estado IN ('PENDIENTE','PROMOVIDO','RECHAZADO')");
                    table.ForeignKey(
                        name: "FK_Plantilla_Importacion_Fila_Plantilla_Importacion_Lote_lote_~",
                        column: x => x.lote_id,
                        principalTable: "Plantilla_Importacion_Lote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Evaluacion_Criterio_item_id_codigo",
                table: "Plantilla_Evaluacion_Criterio",
                columns: ["item_id", "codigo"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Evaluacion_Opcion_plantilla_id_codigo",
                table: "Plantilla_Evaluacion_Opcion",
                columns: ["plantilla_id", "codigo"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Evaluacion_Regla_plantilla_id_codigo",
                table: "Plantilla_Evaluacion_Regla",
                columns: ["plantilla_id", "codigo"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Importacion_Fila_lote_id_codigo",
                table: "Plantilla_Importacion_Fila",
                columns: ["lote_id", "codigo"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Importacion_Lote_archivo_hoja_hash_contenido",
                table: "Plantilla_Importacion_Lote",
                columns: ["archivo", "hoja", "hash_contenido"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Importacion_Lote_plantilla_id",
                table: "Plantilla_Importacion_Lote",
                column: "plantilla_id");

            migrationBuilder.Sql(PlantillaArbol);
            migrationBuilder.Sql(ImportarPlantillaBpm);
            migrationBuilder.Sql(PublicarPlantilla);
            migrationBuilder.Sql(PlantillaPublicadaInmutable);
        }

        private const string PlantillaArbol = """
            CREATE OR REPLACE FUNCTION fn_plantilla_arbol(p_plantilla_id integer)
            RETURNS TABLE (
                item_id integer,
                padre_id integer,
                codigo text,
                descripcion text,
                tipo_item text,
                orden integer,
                nivel integer,
                ruta text)
            LANGUAGE sql
            STABLE
            AS $arbol$
                WITH RECURSIVE arbol (n_id, n_padre, n_codigo, n_descripcion, n_tipo, n_orden, n_nivel, n_ruta) AS (
                    SELECT r."Id", r.padre_id, r.codigo::text, r.descripcion, r.tipo_item::text, r.orden, 1, r.codigo::text
                    FROM "Plantilla_Evaluacion_Item" r
                    WHERE r.plantilla_id = p_plantilla_id AND r.padre_id IS NULL AND r.activo
                    UNION ALL
                    SELECT h."Id", h.padre_id, h.codigo::text, h.descripcion, h.tipo_item::text, h.orden,
                           a.n_nivel + 1, a.n_ruta || ' > ' || h.codigo
                    FROM "Plantilla_Evaluacion_Item" h
                    JOIN arbol a ON a.n_id = h.padre_id
                    WHERE h.plantilla_id = p_plantilla_id AND h.activo
                )
                SELECT n_id, n_padre, n_codigo, n_descripcion, n_tipo, n_orden, n_nivel, n_ruta
                FROM arbol
                ORDER BY n_ruta, n_orden;
            $arbol$;
            """;

        private const string ImportarPlantillaBpm = """
            CREATE OR REPLACE PROCEDURE sp_importar_plantilla_bpm(
                IN p_lote_id integer,
                IN p_usuario_id uuid,
                INOUT p_plantilla_id integer DEFAULT NULL)
            LANGUAGE plpgsql
            AS $importar$
            DECLARE
                v_lote record;
                v_filas integer;
                v_alcanzables integer;
                v_invalidas integer;
            BEGIN
                IF p_usuario_id IS NULL THEN
                    RAISE EXCEPTION 'La importación requiere el usuario responsable.';
                END IF;

                SELECT * INTO v_lote FROM "Plantilla_Importacion_Lote"
                WHERE "Id" = p_lote_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'El lote de importación % no existe.', p_lote_id;
                END IF;
                IF v_lote.estado = 'PROMOVIDO' THEN
                    p_plantilla_id := v_lote.plantilla_id;
                    RETURN;
                END IF;
                IF v_lote.estado <> 'PENDIENTE' THEN
                    RAISE EXCEPTION 'El lote % fue rechazado y no puede promoverse.', p_lote_id;
                END IF;

                SELECT count(*) INTO v_filas FROM "Plantilla_Importacion_Fila" WHERE lote_id = p_lote_id;
                IF v_filas = 0 THEN
                    RAISE EXCEPTION 'El lote % no contiene filas.', p_lote_id;
                END IF;

                SELECT count(*) INTO v_invalidas FROM "Plantilla_Importacion_Fila"
                WHERE lote_id = p_lote_id
                  AND (btrim(descripcion) = ''
                       OR tipo_item NOT IN ('CHAPTER','SECTION','SUBSECTION','GROUP','QUESTION','INSTRUCTION','OPTION'));
                IF v_invalidas > 0 THEN
                    RAISE EXCEPTION 'El lote % tiene % filas sin texto o con un tipo de ítem desconocido.', p_lote_id, v_invalidas;
                END IF;

                SELECT count(*) INTO v_invalidas FROM "Plantilla_Importacion_Fila" h
                WHERE h.lote_id = p_lote_id AND h.codigo_padre IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "Plantilla_Importacion_Fila" p
                                  WHERE p.lote_id = p_lote_id AND p.codigo = h.codigo_padre);
                IF v_invalidas > 0 THEN
                    RAISE EXCEPTION 'El lote % tiene % filas cuyo padre no existe en el mismo lote.', p_lote_id, v_invalidas;
                END IF;

                WITH RECURSIVE alcanzable (a_codigo) AS (
                    SELECT f.codigo FROM "Plantilla_Importacion_Fila" f
                    WHERE f.lote_id = p_lote_id AND f.codigo_padre IS NULL
                    UNION
                    SELECT h.codigo FROM "Plantilla_Importacion_Fila" h
                    JOIN alcanzable a ON h.codigo_padre = a.a_codigo
                    WHERE h.lote_id = p_lote_id
                )
                SELECT count(*) INTO v_alcanzables FROM alcanzable;
                IF v_alcanzables <> v_filas THEN
                    RAISE EXCEPTION 'El árbol del lote % tiene ciclos: solo % de % filas son alcanzables desde una raíz.',
                        p_lote_id, v_alcanzables, v_filas;
                END IF;

                INSERT INTO "Plantilla_Evaluacion" (familia_id, nombre, version, estado, fecha_creacion)
                VALUES (gen_random_uuid(), v_lote.archivo, 1, 'DRAFT', now())
                RETURNING "Id" INTO p_plantilla_id;

                INSERT INTO "Plantilla_Evaluacion_Item" (
                    plantilla_id, padre_id, codigo, descripcion, tipo_item, orden, peso,
                    requerido, critico, permite_no_aplica, tipo_respuesta, reglas,
                    configuracion_puntaje, activo, version_token)
                SELECT p_plantilla_id, NULL, f.codigo, btrim(f.descripcion), f.tipo_item, f.orden,
                       CASE WHEN f.tipo_item = 'QUESTION' THEN 1 END,
                       f.tipo_item = 'QUESTION', false, f.tipo_item = 'QUESTION',
                       CASE WHEN f.tipo_item = 'QUESTION' THEN 'BPM_OPTION' END,
                       jsonb_build_object('hoja', v_lote.hoja, 'celda', f.celda_origen),
                       '{}'::jsonb, true, gen_random_uuid()
                FROM "Plantilla_Importacion_Fila" f
                WHERE f.lote_id = p_lote_id
                ORDER BY f.orden;

                UPDATE "Plantilla_Evaluacion_Item" h
                SET padre_id = p."Id"
                FROM "Plantilla_Importacion_Fila" f, "Plantilla_Evaluacion_Item" p
                WHERE h.plantilla_id = p_plantilla_id AND h.codigo = f.codigo
                  AND f.lote_id = p_lote_id AND f.codigo_padre IS NOT NULL
                  AND p.plantilla_id = p_plantilla_id AND p.codigo = f.codigo_padre;

                INSERT INTO "Plantilla_Evaluacion_Opcion" (plantilla_id, codigo, nombre, valor, cuenta_denominador, orden)
                VALUES (p_plantilla_id, 'C', 'Cumple', 1, true, 1),
                       (p_plantilla_id, 'CP', 'Cumplimiento parcial', 0.5, true, 2),
                       (p_plantilla_id, 'IT', 'Incumplimiento total', 0, true, 3),
                       (p_plantilla_id, 'NA', 'No aplica', NULL, false, 4);

                UPDATE "Plantilla_Importacion_Fila" SET estado = 'PROMOVIDO' WHERE lote_id = p_lote_id;
                UPDATE "Plantilla_Importacion_Lote"
                SET estado = 'PROMOVIDO', plantilla_id = p_plantilla_id, creado_por = coalesce(creado_por, p_usuario_id)
                WHERE "Id" = p_lote_id;
            END;
            $importar$;
            """;

        private const string PublicarPlantilla = """
            CREATE OR REPLACE PROCEDURE sp_publicar_plantilla(
                IN p_plantilla_id integer,
                IN p_usuario_id uuid)
            LANGUAGE plpgsql
            AS $publicar$
            DECLARE
                v_estado text;
                v_preguntas integer;
                v_opciones integer;
                v_invalidas integer;
            BEGIN
                IF p_usuario_id IS NULL THEN
                    RAISE EXCEPTION 'La publicación requiere el usuario responsable.';
                END IF;

                SELECT estado INTO v_estado FROM "Plantilla_Evaluacion"
                WHERE "Id" = p_plantilla_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'La plantilla % no existe.', p_plantilla_id;
                END IF;
                IF v_estado <> 'DRAFT' THEN
                    RAISE EXCEPTION 'La plantilla % ya fue publicada y es inmutable.', p_plantilla_id;
                END IF;

                SELECT count(*) INTO v_preguntas FROM "Plantilla_Evaluacion_Item"
                WHERE plantilla_id = p_plantilla_id AND activo AND tipo_item = 'QUESTION';
                IF v_preguntas = 0 THEN
                    RAISE EXCEPTION 'Una plantilla publicada requiere al menos una pregunta evaluable.';
                END IF;

                SELECT count(*) INTO v_invalidas FROM "Plantilla_Evaluacion_Item"
                WHERE plantilla_id = p_plantilla_id AND activo AND tipo_item = 'QUESTION'
                  AND (peso IS NULL OR peso <= 0);
                IF v_invalidas > 0 THEN
                    RAISE EXCEPTION 'Hay % preguntas sin peso positivo en la plantilla %.', v_invalidas, p_plantilla_id;
                END IF;

                SELECT count(*) INTO v_invalidas FROM "Plantilla_Evaluacion_Item" h
                WHERE h.plantilla_id = p_plantilla_id AND h.activo AND h.padre_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "Plantilla_Evaluacion_Item" p
                                  WHERE p."Id" = h.padre_id AND p.plantilla_id = p_plantilla_id AND p.activo);
                IF v_invalidas > 0 THEN
                    RAISE EXCEPTION 'Hay % ítems cuyo padre no pertenece a la plantilla %.', v_invalidas, p_plantilla_id;
                END IF;

                SELECT count(*) INTO v_opciones FROM "Plantilla_Evaluacion_Opcion"
                WHERE plantilla_id = p_plantilla_id AND codigo IN ('C','CP','IT','NA');
                IF v_opciones <> 4 THEN
                    RAISE EXCEPTION 'La plantilla % debe declarar las cuatro opciones C, CP, IT y NA.', p_plantilla_id;
                END IF;

                UPDATE "Plantilla_Evaluacion"
                SET estado = 'PUBLISHED', fecha_publicacion = now()
                WHERE "Id" = p_plantilla_id;
            END;
            $publicar$;
            """;

        private const string PlantillaPublicadaInmutable = """
            CREATE OR REPLACE FUNCTION fn_plantilla_publicada_inmutable() RETURNS trigger
            LANGUAGE plpgsql
            AS $inmutable$
            DECLARE
                v_fila record;
                v_plantilla integer;
                v_estado text;
            BEGIN
                IF TG_OP = 'DELETE' THEN v_fila := OLD; ELSE v_fila := NEW; END IF;

                IF TG_TABLE_NAME = 'Plantilla_Evaluacion' THEN
                    IF OLD.estado <> 'DRAFT' THEN
                        RAISE EXCEPTION 'Una versión publicada de plantilla de evaluación es inmutable.';
                    END IF;
                    RETURN v_fila;
                END IF;

                IF TG_TABLE_NAME = 'Plantilla_Evaluacion_Criterio' THEN
                    SELECT i.plantilla_id INTO v_plantilla FROM "Plantilla_Evaluacion_Item" i
                    WHERE i."Id" = v_fila.item_id;
                ELSE
                    v_plantilla := v_fila.plantilla_id;
                END IF;

                SELECT estado INTO v_estado FROM "Plantilla_Evaluacion" WHERE "Id" = v_plantilla;
                IF v_estado IS NOT NULL AND v_estado <> 'DRAFT' THEN
                    RAISE EXCEPTION 'Una versión publicada de plantilla de evaluación es inmutable.';
                END IF;
                RETURN v_fila;
            END;
            $inmutable$;

            DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion";
            CREATE TRIGGER tr_plantilla_publicada_inmutable
                BEFORE UPDATE OR DELETE ON "Plantilla_Evaluacion"
                FOR EACH ROW EXECUTE FUNCTION fn_plantilla_publicada_inmutable();

            DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Item";
            CREATE TRIGGER tr_plantilla_publicada_inmutable
                BEFORE INSERT OR UPDATE OR DELETE ON "Plantilla_Evaluacion_Item"
                FOR EACH ROW EXECUTE FUNCTION fn_plantilla_publicada_inmutable();

            DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Opcion";
            CREATE TRIGGER tr_plantilla_publicada_inmutable
                BEFORE INSERT OR UPDATE OR DELETE ON "Plantilla_Evaluacion_Opcion"
                FOR EACH ROW EXECUTE FUNCTION fn_plantilla_publicada_inmutable();

            DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Regla";
            CREATE TRIGGER tr_plantilla_publicada_inmutable
                BEFORE INSERT OR UPDATE OR DELETE ON "Plantilla_Evaluacion_Regla"
                FOR EACH ROW EXECUTE FUNCTION fn_plantilla_publicada_inmutable();

            DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Criterio";
            CREATE TRIGGER tr_plantilla_publicada_inmutable
                BEFORE INSERT OR UPDATE OR DELETE ON "Plantilla_Evaluacion_Criterio"
                FOR EACH ROW EXECUTE FUNCTION fn_plantilla_publicada_inmutable();
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Criterio";
                DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Regla";
                DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Opcion";
                DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion_Item";
                DROP TRIGGER IF EXISTS tr_plantilla_publicada_inmutable ON "Plantilla_Evaluacion";
                DROP FUNCTION IF EXISTS fn_plantilla_publicada_inmutable();
                DROP PROCEDURE IF EXISTS sp_publicar_plantilla(integer, uuid);
                DROP PROCEDURE IF EXISTS sp_importar_plantilla_bpm(integer, uuid, integer);
                DROP FUNCTION IF EXISTS fn_plantilla_arbol(integer);
                """);

            migrationBuilder.DropTable(
                name: "Plantilla_Evaluacion_Criterio");

            migrationBuilder.DropTable(
                name: "Plantilla_Evaluacion_Opcion");

            migrationBuilder.DropTable(
                name: "Plantilla_Evaluacion_Regla");

            migrationBuilder.DropTable(
                name: "Plantilla_Importacion_Fila");

            migrationBuilder.DropTable(
                name: "Plantilla_Importacion_Lote");

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "Plantilla_Evaluacion_Item",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
