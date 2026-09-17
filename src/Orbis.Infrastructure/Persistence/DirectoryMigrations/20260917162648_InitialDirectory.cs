using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbis.Infrastructure.Persistence.DirectoryMigrations;

/// <inheritdoc />
public partial class InitialDirectory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "directory");

        migrationBuilder.CreateTable(
            name: "tenants",
            schema: "directory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenants", x => x.id);
                table.CheckConstraint("ck_tenant_id", "id <> '00000000-0000-0000-0000-000000000000'");
            });

        migrationBuilder.CreateTable(
            name: "users",
            schema: "directory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.id);
                table.CheckConstraint("ck_user_id", "id <> '00000000-0000-0000-0000-000000000000'");
            });

        migrationBuilder.CreateTable(
            name: "tenant_domains",
            schema: "directory",
            columns: table => new
            {
                host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false, collation: "C"),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_verified = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_domains", x => x.host);
                table.ForeignKey(
                    name: "FK_tenant_domains_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalSchema: "directory",
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "external_identities",
            schema: "directory",
            columns: table => new
            {
                issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, collation: "C"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_external_identities", x => new { x.issuer, x.subject });
                table.ForeignKey(
                    name: "FK_external_identities_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "directory",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_external_identities_user_id",
            schema: "directory",
            table: "external_identities",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_tenant_domains_tenant_id",
            schema: "directory",
            table: "tenant_domains",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "external_identities",
            schema: "directory");

        migrationBuilder.DropTable(
            name: "tenant_domains",
            schema: "directory");

        migrationBuilder.DropTable(
            name: "users",
            schema: "directory");

        migrationBuilder.DropTable(
            name: "tenants",
            schema: "directory");
    }
}
