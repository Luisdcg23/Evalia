using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompaniesAndRiskCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categoria_Alimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categoria_Alimento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rnc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factor_Riesgo_Establecimiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    peso = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factor_Riesgo_Establecimiento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nivel_Riesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    puntos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nivel_Riesgo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresa_Usuario",
                columns: table => new
                {
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresa_Usuario", x => new { x.empresa_id, x.usuario_id });
                    table.ForeignKey(
                        name: "FK_Empresa_Usuario_AspNetUsers_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Empresa_Usuario_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Representante_Empresa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Representante_Empresa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Representante_Empresa_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Factor_Riesgo_Opcion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    factor_id = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    puntaje = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factor_Riesgo_Opcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factor_Riesgo_Opcion_Factor_Riesgo_Establecimiento_factor_id",
                        column: x => x.factor_id,
                        principalTable: "Factor_Riesgo_Establecimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Matriz_Frecuencia_Inspeccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    riesgo_min = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    riesgo_min_incluido = table.Column<bool>(type: "boolean", nullable: false),
                    riesgo_max = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    nivel_riesgo_id = table.Column<int>(type: "integer", nullable: false),
                    frecuencia_meses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matriz_Frecuencia_Inspeccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matriz_Frecuencia_Inspeccion_Nivel_Riesgo_nivel_riesgo_id",
                        column: x => x.nivel_riesgo_id,
                        principalTable: "Nivel_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subcategoria_Alimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    categoria_id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    riesgo_micro_id = table.Column<int>(type: "integer", nullable: false),
                    puntaje_micro = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    riesgo_quimi_id = table.Column<int>(type: "integer", nullable: false),
                    puntaje_quimico = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    nivel_riesgo_total = table.Column<int>(type: "integer", nullable: false),
                    puntaje_total = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategoria_Alimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subcategoria_Alimento_Categoria_Alimento_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "Categoria_Alimento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subcategoria_Alimento_Nivel_Riesgo_nivel_riesgo_total",
                        column: x => x.nivel_riesgo_total,
                        principalTable: "Nivel_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subcategoria_Alimento_Nivel_Riesgo_riesgo_micro_id",
                        column: x => x.riesgo_micro_id,
                        principalTable: "Nivel_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subcategoria_Alimento_Nivel_Riesgo_riesgo_quimi_id",
                        column: x => x.riesgo_quimi_id,
                        principalTable: "Nivel_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Establecimiento_Factor_Valor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    factor_id = table.Column<int>(type: "integer", nullable: false),
                    opcion_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_registro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    registrado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Establecimiento_Factor_Valor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Establecimiento_Factor_Valor_AspNetUsers_registrado_por",
                        column: x => x.registrado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Establecimiento_Factor_Valor_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Establecimiento_Factor_Valor_Factor_Riesgo_Establecimiento_~",
                        column: x => x.factor_id,
                        principalTable: "Factor_Riesgo_Establecimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Establecimiento_Factor_Valor_Factor_Riesgo_Opcion_opcion_id",
                        column: x => x.opcion_id,
                        principalTable: "Factor_Riesgo_Opcion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Calculo_Riesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_calculo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    riesgo_producto_rd = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    riesgo_establecimiento_re = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    riesgo_total_rt = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    nivel_riesgo_id = table.Column<int>(type: "integer", nullable: false),
                    matriz_frecuencia_id = table.Column<int>(type: "integer", nullable: false),
                    detalle_factores = table.Column<string>(type: "jsonb", nullable: false),
                    generado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calculo_Riesgo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Calculo_Riesgo_AspNetUsers_generado_por",
                        column: x => x.generado_por,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calculo_Riesgo_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calculo_Riesgo_Matriz_Frecuencia_Inspeccion_matriz_frecuenc~",
                        column: x => x.matriz_frecuencia_id,
                        principalTable: "Matriz_Frecuencia_Inspeccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calculo_Riesgo_Nivel_Riesgo_nivel_riesgo_id",
                        column: x => x.nivel_riesgo_id,
                        principalTable: "Nivel_Riesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Establecimiento_Subcategoria",
                columns: table => new
                {
                    empresa_id = table.Column<int>(type: "integer", nullable: false),
                    subcategoria_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Establecimiento_Subcategoria", x => new { x.empresa_id, x.subcategoria_id });
                    table.ForeignKey(
                        name: "FK_Establecimiento_Subcategoria_Empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Establecimiento_Subcategoria_Subcategoria_Alimento_subcateg~",
                        column: x => x.subcategoria_id,
                        principalTable: "Subcategoria_Alimento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calculo_Riesgo_empresa_id_fecha_calculo",
                table: "Calculo_Riesgo",
                columns: ["empresa_id", "fecha_calculo"]);

            migrationBuilder.CreateIndex(
                name: "IX_Calculo_Riesgo_generado_por",
                table: "Calculo_Riesgo",
                column: "generado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Calculo_Riesgo_matriz_frecuencia_id",
                table: "Calculo_Riesgo",
                column: "matriz_frecuencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_Calculo_Riesgo_nivel_riesgo_id",
                table: "Calculo_Riesgo",
                column: "nivel_riesgo_id");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_Alimento_nombre",
                table: "Categoria_Alimento",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresa_rnc",
                table: "Empresa",
                column: "rnc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresa_Usuario_usuario_id",
                table: "Empresa_Usuario",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_Establecimiento_Factor_Valor_empresa_id_factor_id_vigente",
                table: "Establecimiento_Factor_Valor",
                columns: ["empresa_id", "factor_id", "vigente"]);

            migrationBuilder.CreateIndex(
                name: "IX_Establecimiento_Factor_Valor_factor_id",
                table: "Establecimiento_Factor_Valor",
                column: "factor_id");

            migrationBuilder.CreateIndex(
                name: "IX_Establecimiento_Factor_Valor_opcion_id",
                table: "Establecimiento_Factor_Valor",
                column: "opcion_id");

            migrationBuilder.CreateIndex(
                name: "IX_Establecimiento_Factor_Valor_registrado_por",
                table: "Establecimiento_Factor_Valor",
                column: "registrado_por");

            migrationBuilder.CreateIndex(
                name: "IX_Establecimiento_Subcategoria_subcategoria_id",
                table: "Establecimiento_Subcategoria",
                column: "subcategoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_Factor_Riesgo_Establecimiento_codigo",
                table: "Factor_Riesgo_Establecimiento",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factor_Riesgo_Opcion_factor_id_orden",
                table: "Factor_Riesgo_Opcion",
                columns: ["factor_id", "orden"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matriz_Frecuencia_Inspeccion_nivel_riesgo_id",
                table: "Matriz_Frecuencia_Inspeccion",
                column: "nivel_riesgo_id");

            migrationBuilder.CreateIndex(
                name: "IX_Nivel_Riesgo_nombre",
                table: "Nivel_Riesgo",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Nivel_Riesgo_puntos",
                table: "Nivel_Riesgo",
                column: "puntos",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Representante_Empresa_empresa_id_documento",
                table: "Representante_Empresa",
                columns: ["empresa_id", "documento"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subcategoria_Alimento_categoria_id_nombre",
                table: "Subcategoria_Alimento",
                columns: ["categoria_id", "nombre"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subcategoria_Alimento_nivel_riesgo_total",
                table: "Subcategoria_Alimento",
                column: "nivel_riesgo_total");

            migrationBuilder.CreateIndex(
                name: "IX_Subcategoria_Alimento_riesgo_micro_id",
                table: "Subcategoria_Alimento",
                column: "riesgo_micro_id");

            migrationBuilder.CreateIndex(
                name: "IX_Subcategoria_Alimento_riesgo_quimi_id",
                table: "Subcategoria_Alimento",
                column: "riesgo_quimi_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Calculo_Riesgo");

            migrationBuilder.DropTable(
                name: "Empresa_Usuario");

            migrationBuilder.DropTable(
                name: "Establecimiento_Factor_Valor");

            migrationBuilder.DropTable(
                name: "Establecimiento_Subcategoria");

            migrationBuilder.DropTable(
                name: "Representante_Empresa");

            migrationBuilder.DropTable(
                name: "Matriz_Frecuencia_Inspeccion");

            migrationBuilder.DropTable(
                name: "Factor_Riesgo_Opcion");

            migrationBuilder.DropTable(
                name: "Subcategoria_Alimento");

            migrationBuilder.DropTable(
                name: "Empresa");

            migrationBuilder.DropTable(
                name: "Factor_Riesgo_Establecimiento");

            migrationBuilder.DropTable(
                name: "Categoria_Alimento");

            migrationBuilder.DropTable(
                name: "Nivel_Riesgo");
        }
    }
}
