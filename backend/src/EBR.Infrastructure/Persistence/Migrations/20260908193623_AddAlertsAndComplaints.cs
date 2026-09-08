using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertsAndComplaints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerta_LAPCH",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_alerta = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    fecha_recepcion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    producto = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    resultado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    motivo_decision = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fecha_decision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerta_LAPCH", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerta_LAPCH_AspNetUsers_creado_por",
                        column: x => x.creado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alerta_LAPCH_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Denuncia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo_denuncia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    fecha_recepcion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    denunciante = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    empresa_id = table.Column<int>(type: "integer", nullable: true),
                    resultado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    motivo_decision = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fecha_decision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Denuncia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Denuncia_AspNetUsers_creado_por",
                        column: x => x.creado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Denuncia_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerta_LAPCH_creado_por",
                table: "Alerta_LAPCH",
                column: "creado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Alerta_LAPCH_empresa_id",
                table: "Alerta_LAPCH",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "IX_Alerta_LAPCH_numero_alerta",
                table: "Alerta_LAPCH",
                column: "numero_alerta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alerta_LAPCH_resultado_fecha_recepcion",
                table: "Alerta_LAPCH",
                columns: ["resultado", "fecha_recepcion"]);

            migrationBuilder.CreateIndex(
                name: "IX_Denuncia_creado_por",
                table: "Denuncia",
                column: "creado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Denuncia_empresa_id",
                table: "Denuncia",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "IX_Denuncia_resultado_fecha_recepcion",
                table: "Denuncia",
                columns: ["resultado", "fecha_recepcion"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerta_LAPCH");

            migrationBuilder.DropTable(
                name: "Denuncia");
        }
    }
}
