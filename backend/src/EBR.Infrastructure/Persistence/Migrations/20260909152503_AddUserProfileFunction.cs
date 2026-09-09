using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EBR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PerfilUsuario);
        }

        private const string PerfilUsuario = """
            CREATE OR REPLACE FUNCTION fn_perfil_usuario(p_usuario_id uuid)
            RETURNS TABLE (
                usuario_id uuid,
                correo text,
                nombre_completo text,
                rol text,
                empresa_id integer,
                razon_social text)
            LANGUAGE sql
            STABLE
            AS $perfil$
                SELECT u."Id", u."Email", u."FullName", r."Name", e."Id", e.razon_social
                FROM "AspNetUsers" u
                JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                LEFT JOIN "Empresa_Usuario" eu ON eu.usuario_id = u."Id"
                LEFT JOIN "Empresa" e ON e."Id" = eu.empresa_id
                WHERE u."Id" = p_usuario_id
                ORDER BY e."Id";
            $perfil$;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS fn_perfil_usuario(uuid);
                """);
        }
    }
}
