using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AtomicMembershipAccess : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Falhar sob contenção é preferível a bloquear uma tabela grande sem limite durante o rollout.
        migrationBuilder.Sql("SET LOCAL lock_timeout = '3s'; SET LOCAL statement_timeout = '10s';");
        migrationBuilder.DropCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships");

        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "memberships",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.CreateTable(
            name: "membership_access_changes",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<Guid>(type: "uuid", nullable: false),
                member_id = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                previous_permissions = table.Column<int>(type: "integer", nullable: false),
                previous_is_active = table.Column<bool>(type: "boolean", nullable: false),
                previous_version = table.Column<long>(type: "bigint", nullable: false),
                permissions = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_membership_access_changes", x => new { x.tenant_id, x.actor_id, x.key });
                table.CheckConstraint("ck_member_change_effect", "permissions <> previous_permissions OR is_active <> previous_is_active");
                table.CheckConstraint("ck_member_change_fingerprint", "fingerprint ~ '^[0-9A-F]{64}$'");
                table.CheckConstraint("ck_member_change_key", "key <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_member_change_permissions", "permissions BETWEEN 0 AND 511 AND previous_permissions BETWEEN 0 AND 511");
                table.CheckConstraint("ck_member_change_version", "previous_version > 0 AND version > previous_version AND version - previous_version = 1");
                table.ForeignKey(
                    name: "FK_membership_access_changes_memberships_tenant_id_actor_id",
                    columns: x => new { x.tenant_id, x.actor_id },
                    principalTable: "memberships",
                    principalColumns: new[] { "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_membership_access_changes_memberships_tenant_id_member_id",
                    columns: x => new { x.tenant_id, x.member_id },
                    principalTable: "memberships",
                    principalColumns: new[] { "tenant_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships",
            sql: "permissions BETWEEN 0 AND 511");

        migrationBuilder.AddCheckConstraint(
            name: "ck_membership_version",
            table: "memberships",
            sql: "version > 0");

        migrationBuilder.CreateIndex(
            name: "IX_membership_access_changes_tenant_id_member_id_version",
            table: "membership_access_changes",
            columns: new[] { "tenant_id", "member_id", "version" },
            unique: true);

        migrationBuilder.Sql("""
            ALTER TABLE public.membership_access_changes ENABLE ROW LEVEL SECURITY;
            ALTER TABLE public.membership_access_changes FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON public.membership_access_changes
                USING (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('orbis.tenant_id', true), '')::uuid);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Uma reversão estrutural não pode apagar auditoria, recibos, versões ou concessões administrativas.
        migrationBuilder.Sql("""
            SET LOCAL lock_timeout = '3s'; SET LOCAL statement_timeout = '10s';
            LOCK TABLE public.memberships, public.membership_access_changes IN ACCESS EXCLUSIVE MODE;
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM public.membership_access_changes)
                    OR EXISTS (SELECT 1 FROM public.memberships WHERE version <> 1 OR permissions > 255) THEN
                    RAISE EXCEPTION 'Administrative history or access requires a forward migration.' USING ERRCODE = '23514';
                END IF;
            END $$;
            """);
        migrationBuilder.DropTable(
            name: "membership_access_changes");

        migrationBuilder.DropCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships");

        migrationBuilder.DropCheckConstraint(
            name: "ck_membership_version",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "version",
            table: "memberships");

        migrationBuilder.AddCheckConstraint(
            name: "ck_membership_permissions",
            table: "memberships",
            sql: "permissions BETWEEN 0 AND 255");
    }
}
