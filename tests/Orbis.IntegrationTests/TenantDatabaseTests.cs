using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Domain.WorkOrders;

namespace Orbis.IntegrationTests;

public sealed class TenantDatabaseTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task RlsProtectsQueriesEvenWhenEfFilterIsBypassed()
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var orders = await context.WorkOrders.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        Assert.NotEmpty(orders);
        Assert.All(orders, order => Assert.Equal(database.TenantA, order.TenantId));
        Assert.Null(await context.WorkOrders.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == database.OrderB));
        Assert.All(await context.Memberships.IgnoreQueryFilters().ToListAsync(), member => Assert.Equal(database.TenantA, member.TenantId));
    }

    [Fact]
    public async Task MissingTransactionContextReturnsNoTenantData()
    {
        await using var context = database.CreateContext(database.TenantA);
        Assert.Empty(await context.WorkOrders.IgnoreQueryFilters().ToListAsync());
        context.WorkOrders.Add(WorkOrder.Request(database.TenantA, database.UserA, "Denied", DateTimeOffset.UnixEpoch));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ForgedTenantOnTrackedEntityIsRejectedBeforeWrite()
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        context.WorkOrders.Add(WorkOrder.Request(database.TenantB, database.UserB, "Forged scope", DateTimeOffset.UnixEpoch));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task RlsDeniesRawCrossTenantInsert()
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO work_orders (tenant_id,id,customer_user_id,description,status,version,created_at)
            VALUES ({database.TenantB},{Guid.NewGuid()},{database.UserB},'forbidden',0,1,{DateTimeOffset.UnixEpoch})
            """));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task CrossTenantIdCannotBeUpdatedEvenWithRawSql()
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var changed = await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE work_orders SET description='forged' WHERE id={database.OrderB}");
        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task CompositeForeignKeyRejectsCustomerFromAnotherTenant()
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        context.WorkOrders.Add(WorkOrder.Request(database.TenantA, database.UserB, "Foreign customer", DateTimeOffset.UnixEpoch));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task RuntimeCannotBypassRlsOrOwnTables()
    {
        await using var connection = new NpgsqlConnection(database.RuntimeConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT r.rolsuper OR r.rolbypassrls OR r.rolcreatedb OR r.rolcreaterole
                OR EXISTS (SELECT 1 FROM pg_class c WHERE c.relname IN ('work_orders','memberships') AND c.relowner=r.oid)
            FROM pg_roles r WHERE r.rolname=current_user
            """, connection);
        Assert.False((bool)(await command.ExecuteScalarAsync())!);
        command.CommandText = "SELECT count(*) FROM pg_class WHERE relname IN ('work_orders','memberships') AND relrowsecurity AND relforcerowsecurity";
        Assert.Equal(2L, await command.ExecuteScalarAsync());
        command.CommandText = "TRUNCATE work_orders";
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TenantContextDoesNotSurviveCommitOrRollbackOnReusedConnection(bool commit)
    {
        var settings = new NpgsqlConnectionStringBuilder(database.RuntimeConnection)
        {
            MaxPoolSize = 1,
            ApplicationName = $"pool-isolation-{Guid.NewGuid():N}"
        };
        int firstPid;
        await using (var connection = new NpgsqlConnection(settings.ConnectionString))
        {
            await connection.OpenAsync();
            firstPid = connection.ProcessID;
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("SELECT set_config('orbis.tenant_id', @tenant, true)", connection, transaction);
            command.Parameters.AddWithValue("tenant", database.TenantA.ToString());
            await command.ExecuteNonQueryAsync();
            if (commit) await transaction.CommitAsync();
            else await transaction.RollbackAsync();
        }

        await using var reused = new NpgsqlConnection(settings.ConnectionString);
        await reused.OpenAsync();
        Assert.Equal(firstPid, reused.ProcessID);
        await using var missing = new NpgsqlCommand("SELECT count(*) FROM work_orders", reused);
        Assert.Equal(0L, await missing.ExecuteScalarAsync());
        await using var next = await reused.BeginTransactionAsync();
        await using var scoped = new NpgsqlCommand("SELECT set_config('orbis.tenant_id', @tenant, true)", reused, next);
        scoped.Parameters.AddWithValue("tenant", database.TenantB.ToString());
        await scoped.ExecuteNonQueryAsync();
        scoped.CommandText = "SELECT count(*) FROM work_orders WHERE tenant_id <> @tenant::uuid";
        Assert.Equal(0L, await scoped.ExecuteScalarAsync());
        scoped.CommandText = "SELECT count(*) FROM work_orders";
        Assert.True((long)(await scoped.ExecuteScalarAsync())! > 0);
    }

    [Fact]
    public async Task OneHundredConcurrentUpdatesCannotLoseACommittedTransition()
    {
        Guid orderId;
        await using (var create = database.CreateContext(database.TenantA))
        {
            await using var tx = await create.BeginTenantTransactionAsync();
            var order = WorkOrder.Request(database.TenantA, database.UserA, "Concurrent request", DateTimeOffset.UnixEpoch);
            create.WorkOrders.Add(order);
            await create.SaveChangesAsync();
            await tx.CommitAsync();
            orderId = order.Id;
        }

        // O carregamento termina antes das escritas para que os 100 concorrentes disputem a mesma versão.
        var snapshots = await Task.WhenAll(Enumerable.Range(0, 100).Select(async _ =>
        {
            await using var reader = database.CreateContext(database.TenantA);
            await using var transaction = await reader.BeginTenantTransactionAsync();
            return await reader.WorkOrders.AsNoTracking().SingleAsync(x => x.Id == orderId);
        }));
        var contenders = await Task.WhenAll(snapshots.Select(async order =>
        {
            await using var context = database.CreateContext(database.TenantA);
            await using var transaction = await context.BeginTenantTransactionAsync();
            context.WorkOrders.Attach(order);
            order.Cancel();
            try
            {
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException) { return false; }
        }));
        Assert.Single(contenders, success => success);
        await using var verify = database.CreateContext(database.TenantA);
        await using var verifyTransaction = await verify.BeginTenantTransactionAsync();
        var final = await verify.WorkOrders.AsNoTracking().SingleAsync(x => x.Id == orderId);
        Assert.Equal(WorkOrderStatus.Cancelled, final.Status);
        Assert.Equal(2, final.Version);
    }
}
