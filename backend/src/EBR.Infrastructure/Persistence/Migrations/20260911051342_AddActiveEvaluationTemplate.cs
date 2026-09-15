using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveEvaluationTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plantilla_Activa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    plantilla_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_activacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Activa", x => x.Id);
                    table.CheckConstraint("CK_Plantilla_Activa_Singleton", "\"Id\" = 1");
                    table.ForeignKey(
                        name: "FK_Plantilla_Activa_Plantilla_Evaluacion_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "Plantilla_Evaluacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Activa_plantilla_id",
                table: "Plantilla_Activa",
                column: "plantilla_id");

            // Migración de datos: activa la ficha oficial real para que StartAsync deje de depender de
            // "la publicada más recientemente" (varias plantillas de prueba se publicaron después de
            // ella). Resuelve el id por nombre exacto con una subconsulta en vez de asumir un id fijo,
            // porque el id concreto depende del historial de altas de cada base. Si todavía no existe
            // ninguna plantilla oficial publicada (por ejemplo, una base nueva que aún no importó la
            // ficha), el INSERT ... SELECT simplemente no inserta ninguna fila: StartAsync ya maneja ese
            // caso devolviendo 409 hasta que un administrador active una plantilla.
            migrationBuilder.Sql("""
                INSERT INTO "Plantilla_Activa" ("Id", plantilla_id, fecha_activacion, activado_por)
                SELECT 1, "Id", now(), NULL
                FROM "Plantilla_Evaluacion"
                WHERE nombre = 'Ficha de Inspección BPM' AND estado = 'PUBLISHED'
                ORDER BY version DESC
                LIMIT 1;
                """);

            // Las plantillas de prueba publicadas por error durante el desarrollo de las pantallas de
            // administración de plantillas y de ejecución de campo no pueden pasar a RETIRED: el
            // disparador tr_plantilla_publicada_inmutable bloquea cualquier UPDATE sobre una fila de
            // Plantilla_Evaluacion cuyo estado anterior no sea DRAFT. Esto no es un problema una vez que
            // existe Plantilla_Activa: StartAsync ya no las puede elegir sin importar su estado. Las que
            // sí quedaron en DRAFT (nunca llegaron a publicarse, o son una versión de borrador posterior
            // de una prueba publicada) sí se pueden retirar aquí, porque el disparador solo exige que el
            // estado ANTERIOR sea DRAFT, no que el nuevo lo sea.
            migrationBuilder.Sql("""
                UPDATE "Plantilla_Evaluacion"
                SET estado = 'RETIRED'
                WHERE estado = 'DRAFT'
                  AND (nombre = 'Ficha de Inspección BPM 2024'
                       OR nombre ILIKE 'PRUEBA%'
                       OR nombre ILIKE '%PRUEBA%'
                       OR nombre ILIKE 'CLID_VERIFICACION%'
                       OR nombre ILIKE '[PRUEBA%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plantilla_Activa");
        }
    }
}
