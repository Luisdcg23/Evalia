using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHierarchicalEvaluationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plantilla_Evaluacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_publicacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Evaluacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plantilla_Evaluacion_Item",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantilla_id = table.Column<int>(type: "integer", nullable: false),
                    padre_id = table.Column<int>(type: "integer", nullable: true),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    tipo_item = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    peso = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    requerido = table.Column<bool>(type: "boolean", nullable: false),
                    critico = table.Column<bool>(type: "boolean", nullable: false),
                    permite_no_aplica = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_respuesta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    reglas = table.Column<string>(type: "jsonb", nullable: false),
                    configuracion_puntaje = table.Column<string>(type: "jsonb", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    version_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla_Evaluacion_Item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plantilla_Evaluacion_Item_Plantilla_Evaluacion_Item_padre_id",
                        column: x => x.padre_id,
                        principalTable: "Plantilla_Evaluacion_Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Plantilla_Evaluacion_Item_Plantilla_Evaluacion_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "Plantilla_Evaluacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Evaluacion_familia_id_version",
                table: "Plantilla_Evaluacion",
                columns: ["familia_id", "version"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Evaluacion_Item_padre_id",
                table: "Plantilla_Evaluacion_Item",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Evaluacion_Item_plantilla_id_codigo",
                table: "Plantilla_Evaluacion_Item",
                columns: ["plantilla_id", "codigo"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_Evaluacion_Item_plantilla_id_padre_id_orden",
                table: "Plantilla_Evaluacion_Item",
                columns: ["plantilla_id", "padre_id", "orden"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plantilla_Evaluacion_Item");

            migrationBuilder.DropTable(
                name: "Plantilla_Evaluacion");
        }
    }
}
