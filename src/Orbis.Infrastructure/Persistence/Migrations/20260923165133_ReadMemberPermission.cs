using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ReadMemberPermission : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Expandir a máscara não concede flags. Limites abortam DDL sob contenção ou scan excessivo.
        migrationBuilder.Sql("SET LOCAL lock_timeout = '3s'; SET LOCAL statement_timeout = '10s';");
        migrationBuilder.DropCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships");

        migrationBuilder.AddCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships",
            sql: "permissions BETWEEN 0 AND 255");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverter falha atomicamente se ReadMembers já foi concedida; nunca apagar autorização silenciosamente.
        migrationBuilder.Sql("SET LOCAL lock_timeout = '3s'; SET LOCAL statement_timeout = '10s';");
        migrationBuilder.DropCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships");

        migrationBuilder.AddCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships",
            sql: "permissions BETWEEN 0 AND 127");
    }
}
