using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialTenantBoundary : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "memberships",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                permissions = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_memberships", x => new { x.tenant_id, x.user_id });
                table.CheckConstraint("ck_membership_ids", "tenant_id <> '00000000-0000-0000-0000-000000000000' AND user_id <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_membership_permissions", "permissions BETWEEN 0 AND 127");
            });

        migrationBuilder.CreateTable(
            name: "work_orders",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_work_orders", x => new { x.tenant_id, x.id });
                table.CheckConstraint("ck_order_description", "length(btrim(description)) > 0");
                table.CheckConstraint("ck_order_provider", "status NOT IN (1, 2, 3, 4) OR provider_user_id IS NOT NULL");
                table.CheckConstraint("ck_order_status", "status BETWEEN 0 AND 5");
                table.CheckConstraint("ck_order_version", "version > 0");
                table.ForeignKey(
                    name: "FK_work_orders_memberships_tenant_id_customer_user_id",
                    columns: x => new { x.tenant_id, x.customer_user_id },
                    principalTable: "memberships",
                    principalColumns: new[] { "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_work_orders_memberships_tenant_id_provider_user_id",
                    columns: x => new { x.tenant_id, x.provider_user_id },
                    principalTable: "memberships",
                    principalColumns: new[] { "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_work_orders_tenant_id_created_at_id",
            table: "work_orders",
            columns: new[] { "tenant_id", "created_at", "id" },
            descending: new[] { false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_work_orders_tenant_id_customer_user_id",
            table: "work_orders",
            columns: new[] { "tenant_id", "customer_user_id" });

        migrationBuilder.CreateIndex(
            name: "IX_work_orders_tenant_id_provider_user_id",
            table: "work_orders",
            columns: new[] { "tenant_id", "provider_user_id" });

        // RLS protege inclusive SQL sem filtro EF; contexto ausente falha fechado.
        foreach (var table in new[] { "memberships", "work_orders" })
        {
            migrationBuilder.Sql($"""
                ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON {table}
                    USING (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid);
                """);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "work_orders");

        migrationBuilder.DropTable(
            name: "memberships");
    }
}
