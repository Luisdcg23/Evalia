using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationOfficialReportSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "algoritmo_firma",
                table: "Evaluacion_Informe_Oficial",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "firma_base64",
                table: "Evaluacion_Informe_Oficial",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "huella_clave_publica",
                table: "Evaluacion_Informe_Oficial",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_Informe_Oficial_Firma",
                table: "Evaluacion_Informe_Oficial",
                sql: "char_length(firma_base64) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Evaluacion_Informe_Oficial_Huella",
                table: "Evaluacion_Informe_Oficial",
                sql: "char_length(huella_clave_publica) = 64");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_Informe_Oficial_Firma",
                table: "Evaluacion_Informe_Oficial");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Evaluacion_Informe_Oficial_Huella",
                table: "Evaluacion_Informe_Oficial");

            migrationBuilder.DropColumn(
                name: "algoritmo_firma",
                table: "Evaluacion_Informe_Oficial");

            migrationBuilder.DropColumn(
                name: "firma_base64",
                table: "Evaluacion_Informe_Oficial");

            migrationBuilder.DropColumn(
                name: "huella_clave_publica",
                table: "Evaluacion_Informe_Oficial");
        }
    }
}
