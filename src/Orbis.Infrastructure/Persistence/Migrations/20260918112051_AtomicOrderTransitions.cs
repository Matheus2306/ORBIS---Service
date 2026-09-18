using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AtomicOrderTransitions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("SET LOCAL lock_timeout = '3s';");
        migrationBuilder.DropCheckConstraint(
            name: "ck_order_audit_action",
            table: "work_order_audit");

        migrationBuilder.AddColumn<long>(
            name: "order_version",
            table: "work_order_audit",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.CreateTable(
            name: "order_transition_receipts",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<int>(type: "integer", nullable: false),
                key = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_transition_receipts", x => new { x.tenant_id, x.actor_id, x.action, x.key });
                table.CheckConstraint("ck_transition_action", "action BETWEEN 1 AND 5");
                table.CheckConstraint("ck_transition_fingerprint", "fingerprint ~ '^[0-9A-F]{64}$'");
                table.CheckConstraint("ck_transition_key", "key <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_transition_result", "status BETWEEN 1 AND 5 AND version > 1");
                table.ForeignKey(
                    name: "FK_order_transition_receipts_memberships_tenant_id_actor_id",
                    columns: x => new { x.tenant_id, x.actor_id },
                    principalTable: "memberships",
                    principalColumns: new[] { "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_transition_receipts_work_orders_tenant_id_order_id",
                    columns: x => new { x.tenant_id, x.order_id },
                    principalTable: "work_orders",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_audit_action",
            table: "work_order_audit",
            sql: "action IN ('work-order.requested','work-order.assigned','work-order.accepted','work-order.started','work-order.completed','work-order.cancelled')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_audit_version",
            table: "work_order_audit",
            sql: "order_version > 0");

        migrationBuilder.CreateIndex(
            name: "IX_order_transition_receipts_tenant_id_order_id",
            table: "order_transition_receipts",
            columns: new[] { "tenant_id", "order_id" });
        migrationBuilder.Sql("""
            ALTER TABLE order_transition_receipts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE order_transition_receipts FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON order_transition_receipts
                USING (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_transition_receipts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_order_audit_action",
            table: "work_order_audit");

        migrationBuilder.DropCheckConstraint(
            name: "ck_order_audit_version",
            table: "work_order_audit");

        migrationBuilder.DropColumn(
            name: "order_version",
            table: "work_order_audit");

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_audit_action",
            table: "work_order_audit",
            sql: "action = 'work-order.requested'");
    }
}
