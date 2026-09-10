using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Evidencias de la evaluación en campo (fase 4.1). La tabla guarda solo metadatos —nombre, tipo
    /// MIME, tamaño, hash SHA-256 y clave del objeto—; el binario vive en el almacenamiento de objetos
    /// (MinIO local o sistema de archivos). Las restricciones de comprobación replican en la base el
    /// catálogo cerrado de tipos admitidos y el tamaño máximo, y los disparadores garantizan que una
    /// evidencia solo se adjunte mientras la evaluación sigue abierta y que su metadato no se altere
    /// después, igual que el resto de la cadena probatoria del expediente.
    /// </summary>
    public partial class AddEvaluationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evaluacion_Evidencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instancia_id = table.Column<int>(type: "integer", nullable: false),
                    respuesta_id = table.Column<int>(type: "integer", nullable: true),
                    nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    clave_objeto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_carga = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cargado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_Evidencia", x => x.Id);
                    table.CheckConstraint("CK_Evaluacion_Evidencia_Hash", "char_length(hash) = 64");
                    table.CheckConstraint("CK_Evaluacion_Evidencia_Tamano", "tamano_bytes > 0 AND tamano_bytes <= 15728640");
                    table.CheckConstraint("CK_Evaluacion_Evidencia_Tipo", "tipo_mime IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')");
                    table.ForeignKey(
                        name: "FK_Evaluacion_Evidencia_AspNetUsers_cargado_por",
                        column: x => x.cargado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Evidencia_Evaluacion_Instancia_instancia_id",
                        column: x => x.instancia_id,
                        principalTable: "Evaluacion_Instancia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Evidencia_Evaluacion_Respuesta_respuesta_id",
                        column: x => x.respuesta_id,
                        principalTable: "Evaluacion_Respuesta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Evidencia_cargado_por",
                table: "Evaluacion_Evidencia",
                column: "cargado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Evidencia_clave_objeto",
                table: "Evaluacion_Evidencia",
                column: "clave_objeto",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Evidencia_instancia_id",
                table: "Evaluacion_Evidencia",
                column: "instancia_id");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Evidencia_respuesta_id",
                table: "Evaluacion_Evidencia",
                column: "respuesta_id");

            migrationBuilder.Sql(EvidenciaValida);
            migrationBuilder.Sql(EvidenciaInmutable);
        }

        /// <summary>
        /// Una evidencia solo puede adjuntarse mientras la evaluación sigue en captura y siempre a una
        /// respuesta de la misma instancia. La misma regla vive en el endpoint de subida, porque las
        /// pruebas de integración corren sobre un proveedor en memoria que no ejecuta disparadores.
        /// </summary>
        private const string EvidenciaValida = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_evidencia_valida() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            DECLARE
                v_estado text;
                v_instancia_respuesta integer;
            BEGIN
                SELECT estado INTO v_estado FROM "Evaluacion_Instancia" WHERE "Id" = NEW.instancia_id;
                IF v_estado IS NULL THEN
                    RAISE EXCEPTION 'La instancia de evaluación % no existe.', NEW.instancia_id;
                END IF;
                IF v_estado <> 'IN_PROGRESS' THEN
                    RAISE EXCEPTION 'La evaluación ya fue enviada y no admite nuevas evidencias.';
                END IF;
                IF NEW.respuesta_id IS NOT NULL THEN
                    SELECT instancia_id INTO v_instancia_respuesta
                        FROM "Evaluacion_Respuesta" WHERE "Id" = NEW.respuesta_id;
                    IF v_instancia_respuesta IS DISTINCT FROM NEW.instancia_id THEN
                        RAISE EXCEPTION 'La respuesta % no pertenece a la evaluación %.',
                            NEW.respuesta_id, NEW.instancia_id;
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_evidencia_valida ON "Evaluacion_Evidencia";
            CREATE TRIGGER tr_evaluacion_evidencia_valida
                BEFORE INSERT ON "Evaluacion_Evidencia"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_evidencia_valida();
            """;

        /// <summary>
        /// El metadato de una evidencia es parte del sustento del informe: cambiar el hash o la clave del
        /// objeto rompería la correspondencia con el binario almacenado, así que no admite modificación
        /// ni eliminación. Retirar una evidencia equivocada se resuelve declarándolo en la evaluación,
        /// no borrando el rastro.
        /// </summary>
        private const string EvidenciaInmutable = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_evidencia_inmutable() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            BEGIN
                RAISE EXCEPTION 'Los metadatos de una evidencia no admiten modificación ni eliminación.';
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_evidencia_inmutable ON "Evaluacion_Evidencia";
            CREATE TRIGGER tr_evaluacion_evidencia_inmutable
                BEFORE UPDATE OR DELETE ON "Evaluacion_Evidencia"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_evidencia_inmutable();
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_evaluacion_evidencia_inmutable ON "Evaluacion_Evidencia";
                DROP TRIGGER IF EXISTS tr_evaluacion_evidencia_valida ON "Evaluacion_Evidencia";
                DROP FUNCTION IF EXISTS fn_evaluacion_evidencia_inmutable();
                DROP FUNCTION IF EXISTS fn_evaluacion_evidencia_valida();
                """);

            migrationBuilder.DropTable(
                name: "Evaluacion_Evidencia");
        }
    }
}
