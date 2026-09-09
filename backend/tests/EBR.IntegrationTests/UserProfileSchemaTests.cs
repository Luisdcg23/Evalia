using EBR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EBR.IntegrationTests;

public sealed class UserProfileSchemaTests
{
    [Fact]
    public void MigrationDeliversTheUserProfileFunction()
    {
        using var context = new EbrDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE OR REPLACE FUNCTION fn_perfil_usuario", sql);
    }
}
