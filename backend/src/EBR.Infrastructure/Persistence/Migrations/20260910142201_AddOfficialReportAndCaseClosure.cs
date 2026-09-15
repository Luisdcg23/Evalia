using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialReportAndCaseClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evaluacion_Informe_Oficial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    informe_id = table.Column<int>(type: "integer", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    clave_objeto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fecha_generacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    generado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_Informe_Oficial", x => x.Id);
                    table.CheckConstraint("CK_Evaluacion_Informe_Oficial_Hash", "char_length(hash_sha256) = 64");
                    table.CheckConstraint("CK_Evaluacion_Informe_Oficial_Tamano", "tamano_bytes > 0");
                    table.ForeignKey(
                        name: "FK_Evaluacion_Informe_Oficial_AspNetUsers_generado_por",
                        column: x => x.generado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Informe_Oficial_Evaluacion_Informe_informe_id",
                        column: x => x.informe_id,
                        principalTable: "Evaluacion_Informe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Caso_Cierre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    informe_id = table.Column<int>(type: "integer", nullable: false),
                    informe_oficial_id = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resultado = table.Column<string>(type: "text", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cerrado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caso_Cierre", x => x.Id);
                    table.CheckConstraint("CK_Caso_Cierre_Estado", "estado = 'CLOSED'");
                    table.ForeignKey(
                        name: "FK_Caso_Cierre_AspNetUsers_cerrado_por",
                        column: x => x.cerrado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Cierre_Caso_caso_id",
                        column: x => x.caso_id,
                        principalTable: "Caso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Cierre_Evaluacion_Informe_Oficial_informe_oficial_id",
                        column: x => x.informe_oficial_id,
                        principalTable: "Evaluacion_Informe_Oficial",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Cierre_Evaluacion_Informe_informe_id",
                        column: x => x.informe_id,
                        principalTable: "Evaluacion_Informe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Cierre_caso_id",
                table: "Caso_Cierre",
                column: "caso_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Cierre_cerrado_por",
                table: "Caso_Cierre",
                column: "cerrado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Cierre_informe_id",
                table: "Caso_Cierre",
                column: "informe_id");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Cierre_informe_oficial_id",
                table: "Caso_Cierre",
                column: "informe_oficial_id");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Informe_Oficial_clave_objeto",
                table: "Evaluacion_Informe_Oficial",
                column: "clave_objeto",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Informe_Oficial_generado_por",
                table: "Evaluacion_Informe_Oficial",
                column: "generado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Informe_Oficial_informe_id",
                table: "Evaluacion_Informe_Oficial",
                column: "informe_id",
                unique: true);

            migrationBuilder.Sql(CierreInmutable);
            migrationBuilder.Sql(CerrarExpediente);
        }

        private const string CierreInmutable = """
            CREATE OR REPLACE FUNCTION fn_registro_cierre_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                RAISE EXCEPTION 'El registro oficial no admite modificacion ni eliminacion.';
            END;
            $funcion$;
            CREATE TRIGGER tr_informe_oficial_inmutable
                BEFORE UPDATE OR DELETE ON "Evaluacion_Informe_Oficial"
                FOR EACH ROW EXECUTE FUNCTION fn_registro_cierre_inmutable();
            CREATE TRIGGER tr_caso_cierre_inmutable
                BEFORE UPDATE OR DELETE ON "Caso_Cierre"
                FOR EACH ROW EXECUTE FUNCTION fn_registro_cierre_inmutable();
            """;

        private const string CerrarExpediente = """
            CREATE OR REPLACE PROCEDURE sp_cerrar_expediente(
                IN p_caso_id integer,
                IN p_resultado text,
                IN p_usuario_id uuid)
            LANGUAGE plpgsql AS $procedimiento$
            DECLARE
                v_estado text;
                v_informe_id integer;
                v_oficial_id integer;
            BEGIN
                p_resultado := coalesce(btrim(p_resultado), '');
                IF p_resultado = '' THEN RAISE EXCEPTION 'El resultado del cierre es obligatorio.'; END IF;

                SELECT estado INTO v_estado FROM "Caso" WHERE "Id" = p_caso_id FOR UPDATE;
                IF v_estado IS NULL THEN RAISE EXCEPTION 'El expediente % no existe.', p_caso_id; END IF;
                IF EXISTS (SELECT 1 FROM "Caso_Cierre" WHERE caso_id = p_caso_id) THEN RETURN; END IF;
                IF v_estado <> 'APPROVED' THEN
                    RAISE EXCEPTION 'El expediente en estado % no admite cierre.', v_estado;
                END IF;

                SELECT i."Id" INTO v_informe_id
                  FROM "Evaluacion_Informe" i
                  JOIN "Evaluacion_Instancia" e ON e."Id" = i.instancia_id
                 WHERE e.caso_id = p_caso_id
                 ORDER BY i.version DESC
                 LIMIT 1;
                IF v_informe_id IS NULL OR
                   (SELECT estado FROM "Evaluacion_Informe" WHERE "Id" = v_informe_id) <> 'APPROVED' THEN
                    RAISE EXCEPTION 'La ultima version del informe debe estar aprobada.';
                END IF;
                SELECT "Id" INTO v_oficial_id FROM "Evaluacion_Informe_Oficial" WHERE informe_id = v_informe_id;
                IF v_oficial_id IS NULL THEN RAISE EXCEPTION 'La ultima version aprobada exige PDF oficial.'; END IF;

                INSERT INTO "Caso_Cierre"
                    (caso_id, informe_id, informe_oficial_id, estado, resultado, fecha_cierre, cerrado_por)
                VALUES (p_caso_id, v_informe_id, v_oficial_id, 'CLOSED', p_resultado, now(), p_usuario_id);
                UPDATE "Caso" SET estado = 'CLOSED' WHERE "Id" = p_caso_id;
                INSERT INTO "Caso_Estado_Historial"
                    (caso_id, estado_anterior, estado_nuevo, motivo, fecha_cambio, cambiado_por)
                VALUES (p_caso_id, 'APPROVED', 'CLOSED', p_resultado, now(), p_usuario_id);
            END;
            $procedimiento$;
            REVOKE ALL ON PROCEDURE sp_cerrar_expediente(integer, text, uuid) FROM PUBLIC;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP PROCEDURE IF EXISTS sp_cerrar_expediente(integer, text, uuid);
                DROP TRIGGER IF EXISTS tr_caso_cierre_inmutable ON "Caso_Cierre";
                DROP TRIGGER IF EXISTS tr_informe_oficial_inmutable ON "Evaluacion_Informe_Oficial";
                DROP FUNCTION IF EXISTS fn_registro_cierre_inmutable();
                """);
            migrationBuilder.DropTable(
                name: "Caso_Cierre");

            migrationBuilder.DropTable(
                name: "Evaluacion_Informe_Oficial");
        }
    }
}
