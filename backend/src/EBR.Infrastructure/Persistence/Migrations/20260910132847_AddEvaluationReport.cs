using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Informe de evaluación versionado (fase 5.1, RF-16). Cada emisión inserta una versión nueva y
    /// conserva las anteriores, porque el informe es lo que se comunica al establecimiento y una
    /// corrección posterior no puede borrar lo ya emitido. Las cifras del BPM no se copian aquí: viven
    /// en `Evaluacion_Resultado`, que ya es inmutable. Los disparadores replican en la base las dos
    /// reglas que el endpoint aplica —solo se emite sobre una evaluación enviada y la versión avanza de
    /// uno en uno— y prohíben reescribir el contenido de una versión emitida.
    /// </summary>
    public partial class AddEvaluationReport : Migration
    {
        /// <summary>Índice único que impide dos veces la misma versión de un mismo informe.</summary>
        private static readonly string[] VersionPorInstancia = ["instancia_id", "version"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evaluacion_Informe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instancia_id = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resumen_ejecutivo = table.Column<string>(type: "text", nullable: false),
                    hallazgos = table.Column<string>(type: "text", nullable: false),
                    recomendaciones = table.Column<string>(type: "text", nullable: false),
                    fecha_emision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    emitido_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluacion_Informe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Informe_AspNetUsers_emitido_por",
                        column: x => x.emitido_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluacion_Informe_Evaluacion_Instancia_instancia_id",
                        column: x => x.instancia_id,
                        principalTable: "Evaluacion_Instancia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Informe_emitido_por",
                table: "Evaluacion_Informe",
                column: "emitido_por");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluacion_Informe_instancia_id_version",
                table: "Evaluacion_Informe",
                columns: VersionPorInstancia,
                unique: true);

            migrationBuilder.Sql(InformeValido);
            migrationBuilder.Sql(InformeInmutable);
        }

        /// <summary>
        /// El informe describe una evaluación cerrada: emitirlo con la captura abierta produciría un
        /// documento sobre datos que todavía pueden cambiar. La versión avanza de uno en uno para que el
        /// historial no tenga huecos y no dependa del orden en que lleguen las inserciones.
        /// </summary>
        private const string InformeValido = """
            CREATE OR REPLACE FUNCTION fn_evaluacion_informe_valido() RETURNS trigger
            LANGUAGE plpgsql AS $funcion$
            DECLARE
                v_estado text;
                v_ultima integer;
            BEGIN
                SELECT estado INTO v_estado FROM "Evaluacion_Instancia" WHERE "Id" = NEW.instancia_id;
                IF v_estado IS NULL THEN
                    RAISE EXCEPTION 'La instancia de evaluación % no existe.', NEW.instancia_id;
                END IF;
                IF v_estado <> 'SUBMITTED' THEN
                    RAISE EXCEPTION 'La evaluación todavía no ha sido enviada y no admite informe.';
                END IF;
                SELECT COALESCE(MAX(version), 0) INTO v_ultima
                    FROM "Evaluacion_Informe" WHERE instancia_id = NEW.instancia_id;
                IF NEW.version <> v_ultima + 1 THEN
                    RAISE EXCEPTION 'La versión del informe debe ser %, no %.', v_ultima + 1, NEW.version;
                END IF;
                RETURN NEW;
            END;
            $funcion$;

            DROP TRIGGER IF EXISTS tr_evaluacion_informe_valido ON "Evaluacion_Informe";
            CREATE TRIGGER tr_evaluacion_informe_valido
                BEFORE INSERT ON "Evaluacion_Informe"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_informe_valido();
            """;

        /// <summary>
        /// Una versión emitida ya se comunicó, así que su contenido no se reescribe ni se elimina: una
        /// corrección se publica como versión siguiente. `estado` queda fuera del bloqueo porque el
        /// recorrido de revisión del informe lo hará avanzar sobre la misma fila.
        /// </summary>
        private const string InformeInmutable = """
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

            DROP TRIGGER IF EXISTS tr_evaluacion_informe_inmutable ON "Evaluacion_Informe";
            CREATE TRIGGER tr_evaluacion_informe_inmutable
                BEFORE UPDATE OR DELETE ON "Evaluacion_Informe"
                FOR EACH ROW EXECUTE FUNCTION fn_evaluacion_informe_inmutable();
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_evaluacion_informe_inmutable ON "Evaluacion_Informe";
                DROP TRIGGER IF EXISTS tr_evaluacion_informe_valido ON "Evaluacion_Informe";
                DROP FUNCTION IF EXISTS fn_evaluacion_informe_inmutable();
                DROP FUNCTION IF EXISTS fn_evaluacion_informe_valido();
                """);

            migrationBuilder.DropTable(
                name: "Evaluacion_Informe");
        }
    }
}
