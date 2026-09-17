using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AtomicOrderCreation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("SET LOCAL lock_timeout = '3s';");
        migrationBuilder.CreateTable(
            name: "order_creation_receipts",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_creation_receipts", x => new { x.tenant_id, x.actor_id, x.key });
                table.CheckConstraint("ck_creation_fingerprint", "fingerprint ~ '^[0-9A-F]{64}$'");
                table.CheckConstraint("ck_creation_key", "key <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_order_creation_receipts_memberships_tenant_id_actor_id",
                    columns: x => new { x.tenant_id, x.actor_id },
                    principalTable: "memberships",
                    principalColumns: new[] { "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_creation_receipts_work_orders_tenant_id_order_id",
                    columns: x => new { x.tenant_id, x.order_id },
                    principalTable: "work_orders",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "work_order_audit",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_work_order_audit", x => new { x.tenant_id, x.id });
                table.CheckConstraint("ck_order_audit_action", "action = 'work-order.requested'");
                table.ForeignKey(
                    name: "FK_work_order_audit_memberships_tenant_id_actor_id",
                    columns: x => new { x.tenant_id, x.actor_id },
                    principalTable: "memberships",
                    principalColumns: new[] { "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_work_order_audit_work_orders_tenant_id_order_id",
                    columns: x => new { x.tenant_id, x.order_id },
                    principalTable: "work_orders",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_order_creation_receipts_tenant_id_order_id",
            table: "order_creation_receipts",
            columns: new[] { "tenant_id", "order_id" });

        migrationBuilder.CreateIndex(
            name: "IX_work_order_audit_tenant_id_actor_id",
            table: "work_order_audit",
            columns: new[] { "tenant_id", "actor_id" });

        migrationBuilder.CreateIndex(
            name: "IX_work_order_audit_tenant_id_order_id",
            table: "work_order_audit",
            columns: new[] { "tenant_id", "order_id" });

        // Recibos e auditoria pertencem à mesma fronteira de segurança das ordens.
        foreach (var table in new[] { "order_creation_receipts", "work_order_audit" })
            migrationBuilder.Sql($"""
                ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON {table}
                    USING (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid);
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_creation_receipts");

        migrationBuilder.DropTable(
            name: "work_order_audit");
    }
}
