using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmRequestsAndCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Caso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_origen = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    origen_id = table.Column<int>(type: "integer", nullable: false),
                    prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caso_AspNetUsers_creado_por",
                        column: x => x.creado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Solicitud_BPM",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_establecimiento = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitud_BPM", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Solicitud_BPM_AspNetUsers_creado_por",
                        column: x => x.creado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Solicitud_BPM_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Caso_Estado_Historial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    estado_anterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado_nuevo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    fecha_cambio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cambiado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caso_Estado_Historial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caso_Estado_Historial_AspNetUsers_cambiado_por",
                        column: x => x.cambiado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caso_Estado_Historial_Caso_caso_id",
                        column: x => x.caso_id,
                        principalTable: "Caso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Caso_creado_por",
                table: "Caso",
                column: "creado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_empresa_id",
                table: "Caso",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_estado_prioridad_fecha_creacion",
                table: "Caso",
                columns: ["estado", "prioridad", "fecha_creacion"]);

            migrationBuilder.CreateIndex(
                name: "IX_Caso_tipo_origen_origen_id",
                table: "Caso",
                columns: ["tipo_origen", "origen_id"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Estado_Historial_cambiado_por",
                table: "Caso_Estado_Historial",
                column: "cambiado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Caso_Estado_Historial_caso_id_fecha_cambio",
                table: "Caso_Estado_Historial",
                columns: ["caso_id", "fecha_cambio"]);

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_BPM_creado_por",
                table: "Solicitud_BPM",
                column: "creado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_BPM_empresa_id_estado_fecha_creacion",
                table: "Solicitud_BPM",
                columns: ["empresa_id", "estado", "fecha_creacion"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Caso_Estado_Historial");

            migrationBuilder.DropTable(
                name: "Solicitud_BPM");

            migrationBuilder.DropTable(
                name: "Caso");
        }
    }
}
