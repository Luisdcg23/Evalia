using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedRiskRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Factor_Riesgo_Establecimiento_codigo",
                table: "Factor_Riesgo_Establecimiento");

            migrationBuilder.AlterColumn<int>(
                name: "riesgo_quimi_id",
                table: "Subcategoria_Alimento",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "riesgo_micro_id",
                table: "Subcategoria_Alimento",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "puntaje_total",
                table: "Subcategoria_Alimento",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldPrecision: 8,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "puntaje_quimico",
                table: "Subcategoria_Alimento",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldPrecision: 8,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "puntaje_micro",
                table: "Subcategoria_Alimento",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldPrecision: 8,
                oldScale: 3);

            migrationBuilder.AlterColumn<string>(
                name: "nombre",
                table: "Subcategoria_Alimento",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<int>(
                name: "nivel_riesgo_total",
                table: "Subcategoria_Alimento",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "nivel_escala_id",
                table: "Matriz_Frecuencia_Inspeccion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_regla_id",
                table: "Matriz_Frecuencia_Inspeccion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "activo",
                table: "Factor_Riesgo_Establecimiento",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "version_regla_id",
                table: "Factor_Riesgo_Establecimiento",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nombre",
                table: "Categoria_Alimento",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<int>(
                name: "version_regla_id",
                table: "Calculo_Riesgo",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Escala_Riesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    vigente_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigente_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escala_Riesgo", x => x.Id);
                    table.CheckConstraint("CK_Escala_Vigencia", "vigente_hasta IS NULL OR vigente_hasta > vigente_desde");
                });

            migrationBuilder.CreateTable(
                name: "Escala_Riesgo_Nivel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    escala_id = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    puntaje = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escala_Riesgo_Nivel", x => x.Id);
                    table.CheckConstraint("CK_Nivel_Valido", "codigo IN ('LOW','MEDIUM','HIGH') AND puntaje > 0 AND orden BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Escala_Riesgo_Nivel_Escala_Riesgo_escala_id",
                        column: x => x.escala_id,
                        principalTable: "Escala_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Version_Regla_Riesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    version = table.Column<int>(type: "integer", nullable: false),
                    vigente_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigente_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false),
                    metodo_agregacion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    escala_producto_id = table.Column<int>(type: "integer", nullable: false),
                    escala_frecuencia_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Version_Regla_Riesgo", x => x.Id);
                    table.CheckConstraint("CK_Regla_Valida", "metodo_agregacion = 'MAX' AND (vigente_hasta IS NULL OR vigente_hasta > vigente_desde)");
                    table.ForeignKey(
                        name: "FK_Version_Regla_Riesgo_Escala_Riesgo_escala_frecuencia_id",
                        column: x => x.escala_frecuencia_id,
                        principalTable: "Escala_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Version_Regla_Riesgo_Escala_Riesgo_escala_producto_id",
                        column: x => x.escala_producto_id,
                        principalTable: "Escala_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subcategoria_Alimento_Peligro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subcategoria_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_peligro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nivel_escala_id = table.Column<int>(type: "integer", nullable: false),
                    puntaje_propio = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategoria_Alimento_Peligro", x => x.Id);
                    table.CheckConstraint("CK_Peligro_Valido", "tipo_peligro IN ('MICROBIOLOGICAL','CHEMICAL') AND (puntaje_propio IS NULL OR puntaje_propio > 0)");
                    table.ForeignKey(
                        name: "FK_Subcategoria_Alimento_Peligro_Escala_Riesgo_Nivel_nivel_esc~",
                        column: x => x.nivel_escala_id,
                        principalTable: "Escala_Riesgo_Nivel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subcategoria_Alimento_Peligro_Subcategoria_Alimento_subcate~",
                        column: x => x.subcategoria_id,
                        principalTable: "Subcategoria_Alimento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matriz_Frecuencia_Inspeccion_nivel_escala_id",
                table: "Matriz_Frecuencia_Inspeccion",
                column: "nivel_escala_id");

            migrationBuilder.CreateIndex(
                name: "IX_Matriz_Frecuencia_Inspeccion_version_regla_id",
                table: "Matriz_Frecuencia_Inspeccion",
                column: "version_regla_id");

            migrationBuilder.CreateIndex(
                name: "IX_Factor_Riesgo_Establecimiento_version_regla_id_codigo",
                table: "Factor_Riesgo_Establecimiento",
                columns: ["version_regla_id", "codigo"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Calculo_Riesgo_version_regla_id",
                table: "Calculo_Riesgo",
                column: "version_regla_id");

            migrationBuilder.CreateIndex(
                name: "IX_Escala_Riesgo_codigo_version",
                table: "Escala_Riesgo",
                columns: ["codigo", "version"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escala_Riesgo_Nivel_escala_id_codigo",
                table: "Escala_Riesgo_Nivel",
                columns: ["escala_id", "codigo"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escala_Riesgo_Nivel_escala_id_orden",
                table: "Escala_Riesgo_Nivel",
                columns: ["escala_id", "orden"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subcategoria_Alimento_Peligro_nivel_escala_id",
                table: "Subcategoria_Alimento_Peligro",
                column: "nivel_escala_id");

            migrationBuilder.CreateIndex(
                name: "IX_Subcategoria_Alimento_Peligro_subcategoria_id_tipo_peligro",
                table: "Subcategoria_Alimento_Peligro",
                columns: ["subcategoria_id", "tipo_peligro"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Version_Regla_Riesgo_escala_frecuencia_id",
                table: "Version_Regla_Riesgo",
                column: "escala_frecuencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_Version_Regla_Riesgo_escala_producto_id",
                table: "Version_Regla_Riesgo",
                column: "escala_producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_Version_Regla_Riesgo_version",
                table: "Version_Regla_Riesgo",
                column: "version",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Calculo_Riesgo_Version_Regla_Riesgo_version_regla_id",
                table: "Calculo_Riesgo",
                column: "version_regla_id",
                principalTable: "Version_Regla_Riesgo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Factor_Riesgo_Establecimiento_Version_Regla_Riesgo_version_~",
                table: "Factor_Riesgo_Establecimiento",
                column: "version_regla_id",
                principalTable: "Version_Regla_Riesgo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matriz_Frecuencia_Inspeccion_Escala_Riesgo_Nivel_nivel_esca~",
                table: "Matriz_Frecuencia_Inspeccion",
                column: "nivel_escala_id",
                principalTable: "Escala_Riesgo_Nivel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matriz_Frecuencia_Inspeccion_Version_Regla_Riesgo_version_r~",
                table: "Matriz_Frecuencia_Inspeccion",
                column: "version_regla_id",
                principalTable: "Version_Regla_Riesgo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(RegistrarCalculoRiesgo);
            migrationBuilder.Sql(CalculoRiesgoInmutable);
        }

        private const string RegistrarCalculoRiesgo = """
            CREATE OR REPLACE PROCEDURE sp_registrar_calculo_riesgo(
                IN p_empresa_id integer,
                IN p_version_regla_id integer,
                IN p_riesgo_producto numeric,
                IN p_riesgo_establecimiento numeric,
                IN p_detalle_factores jsonb,
                IN p_generado_por uuid,
                INOUT p_calculo_id integer DEFAULT NULL)
            LANGUAGE plpgsql
            AS $procedimiento$
            DECLARE
                v_momento timestamptz := now();
                v_regla record;
                v_factores integer;
                v_pesos numeric;
                v_riesgo_total numeric(8,3);
                v_banda record;
            BEGIN
                IF p_riesgo_producto IS NULL OR p_riesgo_producto <= 0 THEN
                    RAISE EXCEPTION 'El riesgo del producto debe provenir de un peligro conocido y ser positivo.';
                END IF;
                IF p_riesgo_establecimiento IS NULL OR p_riesgo_establecimiento <= 0 THEN
                    RAISE EXCEPTION 'El riesgo del establecimiento debe ser positivo.';
                END IF;
                IF p_detalle_factores IS NULL THEN
                    RAISE EXCEPTION 'El detalle de factores del cálculo es obligatorio.';
                END IF;

                SELECT * INTO v_regla FROM "Version_Regla_Riesgo"
                WHERE "Id" = p_version_regla_id FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'La versión de reglas de riesgo % no existe.', p_version_regla_id;
                END IF;
                IF NOT v_regla.publicado OR NOT v_regla.activo THEN
                    RAISE EXCEPTION 'La versión de reglas de riesgo % no está publicada y vigente.', p_version_regla_id;
                END IF;
                IF v_regla.vigente_desde > v_momento OR
                    (v_regla.vigente_hasta IS NOT NULL AND v_regla.vigente_hasta <= v_momento) THEN
                    RAISE EXCEPTION 'La versión de reglas de riesgo % no está vigente en la fecha del cálculo.', p_version_regla_id;
                END IF;

                SELECT count(*), coalesce(sum(peso), 0) INTO v_factores, v_pesos
                FROM "Factor_Riesgo_Establecimiento"
                WHERE version_regla_id = p_version_regla_id AND activo;
                IF v_factores <> 6 OR v_pesos <> 1 THEN
                    RAISE EXCEPTION 'La versión % requiere seis factores activos cuyos pesos sumen exactamente 1.', p_version_regla_id;
                END IF;

                v_riesgo_total := round(p_riesgo_producto * p_riesgo_establecimiento, 3);
                SELECT m."Id" AS matriz_id, m.nivel_riesgo_id, m.frecuencia_meses INTO v_banda
                FROM "Matriz_Frecuencia_Inspeccion" m
                WHERE m.version_regla_id = p_version_regla_id
                  AND (CASE WHEN m.riesgo_min_incluido THEN v_riesgo_total >= m.riesgo_min ELSE v_riesgo_total > m.riesgo_min END)
                  AND v_riesgo_total <= m.riesgo_max;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'El riesgo total % no pertenece a ningún intervalo de frecuencia de la versión %.',
                        v_riesgo_total, p_version_regla_id;
                END IF;

                INSERT INTO "Calculo_Riesgo" (
                    version_regla_id, empresa_id, fecha_calculo, riesgo_producto_rd,
                    riesgo_establecimiento_re, riesgo_total_rt, nivel_riesgo_id,
                    matriz_frecuencia_id, detalle_factores, generado_por)
                VALUES (
                    p_version_regla_id, p_empresa_id, v_momento, p_riesgo_producto,
                    p_riesgo_establecimiento, v_riesgo_total, v_banda.nivel_riesgo_id,
                    v_banda.matriz_id, p_detalle_factores, p_generado_por)
                RETURNING "Id" INTO p_calculo_id;
            END;
            $procedimiento$;
            """;

        private const string CalculoRiesgoInmutable = """
            CREATE OR REPLACE FUNCTION fn_calculo_riesgo_inmutable() RETURNS trigger
            LANGUAGE plpgsql
            AS $inmutable$
            BEGIN
                RAISE EXCEPTION 'Un cálculo de riesgo registrado es inmutable.';
            END;
            $inmutable$;

            DROP TRIGGER IF EXISTS tr_calculo_riesgo_inmutable ON "Calculo_Riesgo";
            CREATE TRIGGER tr_calculo_riesgo_inmutable
                BEFORE UPDATE OR DELETE ON "Calculo_Riesgo"
                FOR EACH ROW EXECUTE FUNCTION fn_calculo_riesgo_inmutable();
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_calculo_riesgo_inmutable ON \"Calculo_Riesgo\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_calculo_riesgo_inmutable();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_registrar_calculo_riesgo(integer, integer, numeric, numeric, jsonb, uuid, integer);");

            migrationBuilder.DropForeignKey(
                name: "FK_Calculo_Riesgo_Version_Regla_Riesgo_version_regla_id",
                table: "Calculo_Riesgo");

            migrationBuilder.DropForeignKey(
                name: "FK_Factor_Riesgo_Establecimiento_Version_Regla_Riesgo_version_~",
                table: "Factor_Riesgo_Establecimiento");

            migrationBuilder.DropForeignKey(
                name: "FK_Matriz_Frecuencia_Inspeccion_Escala_Riesgo_Nivel_nivel_esca~",
                table: "Matriz_Frecuencia_Inspeccion");

            migrationBuilder.DropForeignKey(
                name: "FK_Matriz_Frecuencia_Inspeccion_Version_Regla_Riesgo_version_r~",
                table: "Matriz_Frecuencia_Inspeccion");

            migrationBuilder.DropTable(
                name: "Subcategoria_Alimento_Peligro");

            migrationBuilder.DropTable(
                name: "Version_Regla_Riesgo");

            migrationBuilder.DropTable(
                name: "Escala_Riesgo_Nivel");

            migrationBuilder.DropTable(
                name: "Escala_Riesgo");

            migrationBuilder.DropIndex(
                name: "IX_Matriz_Frecuencia_Inspeccion_nivel_escala_id",
                table: "Matriz_Frecuencia_Inspeccion");

            migrationBuilder.DropIndex(
                name: "IX_Matriz_Frecuencia_Inspeccion_version_regla_id",
                table: "Matriz_Frecuencia_Inspeccion");

            migrationBuilder.DropIndex(
                name: "IX_Factor_Riesgo_Establecimiento_version_regla_id_codigo",
                table: "Factor_Riesgo_Establecimiento");

            migrationBuilder.DropIndex(
                name: "IX_Calculo_Riesgo_version_regla_id",
                table: "Calculo_Riesgo");

            migrationBuilder.DropColumn(
                name: "nivel_escala_id",
                table: "Matriz_Frecuencia_Inspeccion");

            migrationBuilder.DropColumn(
                name: "version_regla_id",
                table: "Matriz_Frecuencia_Inspeccion");

            migrationBuilder.DropColumn(
                name: "activo",
                table: "Factor_Riesgo_Establecimiento");

            migrationBuilder.DropColumn(
                name: "version_regla_id",
                table: "Factor_Riesgo_Establecimiento");

            migrationBuilder.DropColumn(
                name: "version_regla_id",
                table: "Calculo_Riesgo");

            migrationBuilder.AlterColumn<int>(
                name: "riesgo_quimi_id",
                table: "Subcategoria_Alimento",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "riesgo_micro_id",
                table: "Subcategoria_Alimento",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "puntaje_total",
                table: "Subcategoria_Alimento",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldPrecision: 8,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "puntaje_quimico",
                table: "Subcategoria_Alimento",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldPrecision: 8,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "puntaje_micro",
                table: "Subcategoria_Alimento",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldPrecision: 8,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nombre",
                table: "Subcategoria_Alimento",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "nivel_riesgo_total",
                table: "Subcategoria_Alimento",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nombre",
                table: "Categoria_Alimento",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Factor_Riesgo_Establecimiento_codigo",
                table: "Factor_Riesgo_Establecimiento",
                column: "codigo",
                unique: true);
        }
    }
}
