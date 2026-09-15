using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationAndBpmRequestDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documento_Registro_Usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    referencia_almacenamiento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fecha_carga = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documento_Registro_Usuario", x => x.Id);
                    table.CheckConstraint("CK_Documento_Registro_Tipo", "tipo_documento IN ('CARTA_AUTORIZACION')");
                    table.ForeignKey(
                        name: "FK_Documento_Registro_Usuario_AspNetUsers_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Solicitud_BPM_Documento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    solicitud_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    referencia_almacenamiento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fecha_carga = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitud_BPM_Documento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Solicitud_BPM_Documento_Solicitud_BPM_solicitud_id",
                        column: x => x.solicitud_id,
                        principalTable: "Solicitud_BPM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documento_Registro_Usuario_usuario_id_tipo_documento",
                table: "Documento_Registro_Usuario",
                columns: ["usuario_id", "tipo_documento"]);

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_BPM_Documento_solicitud_id_fecha_carga",
                table: "Solicitud_BPM_Documento",
                columns: ["solicitud_id", "fecha_carga"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documento_Registro_Usuario");

            migrationBuilder.DropTable(
                name: "Solicitud_BPM_Documento");
        }
    }
}
