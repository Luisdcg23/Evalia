using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfileAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_representante",
                table: "Representante_Empresa",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CONTACTO_PRINCIPAL");

            migrationBuilder.AddColumn<string>(
                name: "actividad_economica",
                table: "Empresa",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "correo",
                table: "Empresa",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "direccion",
                table: "Empresa",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "municipio",
                table: "Empresa",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "provincia",
                table: "Empresa",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "telefono",
                table: "Empresa",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "version_token",
                table: "Empresa",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Empresa_Historial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_cambio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cambiado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    cambios = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresa_Historial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Empresa_Historial_AspNetUsers_cambiado_por",
                        column: x => x.cambiado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Empresa_Historial_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Representante_Empresa_empresa_id_tipo_representante_vigente",
                table: "Representante_Empresa",
                columns: ["empresa_id", "tipo_representante"],
                unique: true,
                filter: "vigente");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Representante_Tipo",
                table: "Representante_Empresa",
                sql: "tipo_representante IN ('LEGAL','CALIDAD','CONTACTO_PRINCIPAL')");

            migrationBuilder.CreateIndex(
                name: "IX_Empresa_Historial_cambiado_por",
                table: "Empresa_Historial",
                column: "cambiado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Empresa_Historial_empresa_id_fecha_cambio",
                table: "Empresa_Historial",
                columns: ["empresa_id", "fecha_cambio"]);

            migrationBuilder.Sql(EmpresaHistorialInmutable);
        }

        private const string EmpresaHistorialInmutable = """
            CREATE OR REPLACE FUNCTION fn_empresa_historial_inmutable() RETURNS trigger
            LANGUAGE plpgsql
            AS $inmutable$
            BEGIN
                RAISE EXCEPTION 'El historial de una empresa es append-only.';
            END;
            $inmutable$;

            DROP TRIGGER IF EXISTS tr_empresa_historial_inmutable ON "Empresa_Historial";
            CREATE TRIGGER tr_empresa_historial_inmutable
                BEFORE UPDATE OR DELETE ON "Empresa_Historial"
                FOR EACH ROW EXECUTE FUNCTION fn_empresa_historial_inmutable();
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_empresa_historial_inmutable ON \"Empresa_Historial\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_empresa_historial_inmutable();");

            migrationBuilder.DropTable(
                name: "Empresa_Historial");

            migrationBuilder.DropIndex(
                name: "IX_Representante_Empresa_empresa_id_tipo_representante_vigente",
                table: "Representante_Empresa");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Representante_Tipo",
                table: "Representante_Empresa");

            migrationBuilder.DropColumn(
                name: "tipo_representante",
                table: "Representante_Empresa");

            migrationBuilder.DropColumn(
                name: "actividad_economica",
                table: "Empresa");

            migrationBuilder.DropColumn(
                name: "correo",
                table: "Empresa");

            migrationBuilder.DropColumn(
                name: "direccion",
                table: "Empresa");

            migrationBuilder.DropColumn(
                name: "municipio",
                table: "Empresa");

            migrationBuilder.DropColumn(
                name: "provincia",
                table: "Empresa");

            migrationBuilder.DropColumn(
                name: "telefono",
                table: "Empresa");

            migrationBuilder.DropColumn(
                name: "version_token",
                table: "Empresa");
        }
    }
}
