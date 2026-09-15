using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalReadsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notificacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    destinatario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    titulo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    mensaje = table.Column<string>(type: "text", nullable: false),
                    tipo_referencia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    referencia_id = table.Column<int>(type: "integer", nullable: true),
                    operacion_id = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_lectura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificacion_AspNetUsers_destinatario_id",
                        column: x => x.destinatario_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_destinatario_id_fecha_creacion",
                table: "Notificacion",
                columns: ["destinatario_id", "fecha_creacion"]);

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_operacion_id",
                table: "Notificacion",
                column: "operacion_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notificacion");
        }
    }
}
