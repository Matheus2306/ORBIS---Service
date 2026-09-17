using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class LinkDirectory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Não inventar identidade/domínio para dados legados: preservar IDs com acesso suspenso.
        // row_security=off exige privilégio operacional e impede backfill silenciosamente parcial.
        migrationBuilder.Sql("""
            SET LOCAL lock_timeout = '3s';
            SET LOCAL row_security = off;
            INSERT INTO directory.tenants (id,name,is_active)
                SELECT DISTINCT tenant_id,'Unprovisioned legacy tenant',false FROM memberships
                ON CONFLICT (id) DO NOTHING;
            INSERT INTO directory.users (id,is_active)
                SELECT DISTINCT user_id,false FROM memberships ON CONFLICT (id) DO NOTHING;
            CREATE INDEX ix_memberships_global_user ON memberships (user_id);
            ALTER TABLE memberships ADD CONSTRAINT fk_membership_directory_tenant
                FOREIGN KEY (tenant_id) REFERENCES directory.tenants(id) ON DELETE RESTRICT;
            ALTER TABLE memberships ADD CONSTRAINT fk_membership_directory_user
                FOREIGN KEY (user_id) REFERENCES directory.users(id) ON DELETE RESTRICT;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback do vínculo não apaga identidades ou tenants já provisionados.
        migrationBuilder.Sql("""
            ALTER TABLE memberships DROP CONSTRAINT fk_membership_directory_user;
            ALTER TABLE memberships DROP CONSTRAINT fk_membership_directory_tenant;
            DROP INDEX ix_memberships_global_user;
            """);
    }
}
