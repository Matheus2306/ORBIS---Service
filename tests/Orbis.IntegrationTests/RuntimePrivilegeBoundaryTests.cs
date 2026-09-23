using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Api;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class RuntimePrivilegeBoundaryTests(DatabaseFixture database)
{
    [Theory]
    [InlineData("column-insert")]
    [InlineData("audit-update")]
    [InlineData("directory-update")]
    [InlineData("order-delete")]
    [InlineData("trigger")]
    [InlineData("column-delegation")]
    [InlineData("missing-read")]
    [InlineData("missing-write")]
    [InlineData("administrative-history")]
    public async Task CapabilityMatrixRejectsExcessOrMissingGrants(string scenario)
    {
        var (change, restore) = scenario switch
        {
            "column-insert" => ("GRANT INSERT (tenant_id) ON public.memberships TO orbis_runtime", "REVOKE INSERT (tenant_id) ON public.memberships FROM orbis_runtime"),
            "audit-update" => ("GRANT UPDATE (action) ON public.work_order_audit TO orbis_runtime", "REVOKE UPDATE (action) ON public.work_order_audit FROM orbis_runtime"),
            "directory-update" => ("GRANT UPDATE (is_active) ON directory.tenants TO orbis_runtime", "REVOKE UPDATE (is_active) ON directory.tenants FROM orbis_runtime"),
            "order-delete" => ("GRANT DELETE ON public.work_orders TO orbis_runtime", "REVOKE DELETE ON public.work_orders FROM orbis_runtime"),
            "trigger" => ("GRANT TRIGGER ON public.work_orders TO orbis_runtime", "REVOKE TRIGGER ON public.work_orders FROM orbis_runtime"),
            "column-delegation" => ("GRANT SELECT (permissions) ON public.memberships TO orbis_runtime WITH GRANT OPTION", "REVOKE SELECT (permissions) ON public.memberships FROM orbis_runtime"),
            "missing-read" => ("REVOKE SELECT ON directory.users FROM orbis_runtime", "GRANT SELECT ON directory.users TO orbis_runtime"),
            "missing-write" => ("REVOKE INSERT ON public.order_creation_receipts FROM orbis_runtime", "GRANT INSERT ON public.order_creation_receipts TO orbis_runtime"),
            "administrative-history" => ("GRANT SELECT (actor_id) ON public.membership_access_changes TO orbis_runtime", "REVOKE SELECT (actor_id) ON public.membership_access_changes FROM orbis_runtime"),
            _ => throw new ArgumentException("Unknown grant scenario.", nameof(scenario))
        };
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var source = NpgsqlDataSource.Create(database.RuntimeConnection);
        using var check = new RuntimeDatabaseCheck(source, TimeProvider.System);
        await check.StartAsync(CancellationToken.None);
        await admin.Database.ExecuteSqlRawAsync(change);
        try { await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None)); }
        finally { await admin.Database.ExecuteSqlRawAsync(restore); }
        await check.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ColumnMutationCannotEvadeStartupGuard()
    {
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var source = NpgsqlDataSource.Create(database.RuntimeConnection);
        using var check = new RuntimeDatabaseCheck(source, TimeProvider.System);
        await admin.Database.ExecuteSqlRawAsync("GRANT UPDATE (permissions) ON public.memberships TO orbis_runtime");
        try
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var inspect = new NpgsqlCommand("SELECT has_table_privilege(current_user, 'public.memberships', 'UPDATE'), has_any_column_privilege(current_user, 'public.memberships', 'UPDATE')", connection);
            await using (var result = await inspect.ExecuteReaderAsync())
            {
                Assert.True(await result.ReadAsync());
                Assert.False(result.GetBoolean(0));
                Assert.True(result.GetBoolean(1));
            }
            await using (var runtime = database.CreateContext(database.TenantA))
            {
                await using var transaction = await runtime.BeginTenantTransactionAsync();
                // Demonstra escrita real permitida pela coluna; rollback preserva os dados da fixture.
                Assert.Equal(1, await runtime.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.memberships SET permissions=permissions WHERE user_id={database.UserA}"));
                await transaction.RollbackAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));
        }
        finally { await admin.Database.ExecuteSqlRawAsync("REVOKE UPDATE (permissions) ON public.memberships FROM orbis_runtime"); }
        await check.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RoleWithSetButWithoutInheritanceIsRejected()
    {
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var source = NpgsqlDataSource.Create(database.RuntimeConnection);
        using var check = new RuntimeDatabaseCheck(source, TimeProvider.System);
        // Identificadores gerados internamente; nenhuma entrada de HTTP participa do DDL de teste.
        var writer = "orbis_writer_" + Guid.NewGuid().ToString("N");
        var bridge = "orbis_bridge_" + Guid.NewGuid().ToString("N");
        await admin.Database.OpenConnectionAsync();
        await ExecuteDdl($"CREATE ROLE {writer} NOLOGIN; CREATE ROLE {bridge} NOLOGIN");
        try
        {
            await ExecuteDdl($"""
                GRANT USAGE ON SCHEMA public TO {writer};
                GRANT SELECT,UPDATE ON public.memberships TO {writer};
                GRANT {writer} TO {bridge} WITH INHERIT FALSE, SET TRUE;
                GRANT {bridge} TO orbis_runtime WITH INHERIT FALSE, SET TRUE;
                """);
            await using var connection = await source.OpenConnectionAsync();
            await using (var inspect = new NpgsqlCommand("SELECT pg_has_role(current_user,@role,'USAGE'), pg_has_role(current_user,@role,'SET')", connection))
            {
                inspect.Parameters.AddWithValue("role", writer);
                await using var result = await inspect.ExecuteReaderAsync();
                Assert.True(await result.ReadAsync());
                Assert.False(result.GetBoolean(0));
                Assert.True(result.GetBoolean(1));
            }
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await using var change = new NpgsqlCommand($"SET LOCAL ROLE {writer}; SELECT current_user", connection, transaction);
                Assert.Equal(writer, await change.ExecuteScalarAsync());
                await transaction.RollbackAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));
            await ExecuteDdl($"REVOKE {bridge} FROM orbis_runtime; GRANT {bridge} TO orbis_runtime WITH INHERIT FALSE, SET FALSE, ADMIN TRUE");
            await using (var inspect = new NpgsqlCommand("SELECT pg_has_role(current_user,@role,'SET')", connection))
            {
                inspect.Parameters.AddWithValue("role", bridge);
                Assert.Equal(false, await inspect.ExecuteScalarAsync());
            }
            // ADMIN pode alterar delegação posteriormente; nem memberships sem SET/INHERIT pertencem ao runtime.
            await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));
        }
        finally
        {
            await ExecuteDdl($"""
                REVOKE {bridge} FROM orbis_runtime;
                REVOKE {writer} FROM {bridge};
                REVOKE ALL ON public.memberships FROM {writer};
                REVOKE USAGE ON SCHEMA public FROM {writer};
                DROP ROLE {bridge}; DROP ROLE {writer};
                """);
        }
        await check.StartAsync(CancellationToken.None);

        async Task ExecuteDdl(string sql)
        {
            // DDL não parametriza identificadores; os únicos identificadores variáveis acima são UUIDs internos.
            await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)admin.Database.GetDbConnection());
            await command.ExecuteNonQueryAsync();
        }
    }
}
