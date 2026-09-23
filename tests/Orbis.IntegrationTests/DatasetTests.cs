using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.DataGenerator;
using Orbis.Domain.WorkOrders;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;
using Orbis.QueryProbe;
using Xunit.Abstractions;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class DatasetTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly List<string> temporaryConnections = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (temporaryConnections.Count == 0) return;
        var names = temporaryConnections.Select(connection => new NpgsqlConnectionStringBuilder(connection).Database!).Distinct().ToArray();
        var observerSettings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ORBIS_TEST_ADMIN_CONNECTION")!) { Pooling = false };
        await using var observer = new NpgsqlConnection(observerSettings.ConnectionString);
        await observer.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = ANY (@databases)", observer);
        count.Parameters.AddWithValue("databases", names);
        var before = (long)(await count.ExecuteScalarAsync())!;
        // Cada base descartável tem pools próprios; encerrar só os deste teste preserva testes reais de pooling da API.
        foreach (var connectionString in temporaryConnections.Distinct(StringComparer.Ordinal))
        {
            await using var connection = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(connection);
        }
        long remaining = before;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            remaining = (long)(await count.ExecuteScalarAsync())!;
            if (remaining == 0) break;
            await Task.Delay(50);
        }
        output.WriteLine($"Temporary database connections before/after owned pool cleanup: {before}/{remaining} across {names.Length} databases.");
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task MemberPermissionMigrationPreservesSmallDatasetBoundsLockWaitAndRejectsLossyRollback()
    {
        var connectionString = await NewDatabaseAsync();
        var recipe = new DatasetRecipe(DatasetLevel.Small, DatasetProfile.Uniform, 42);
        await DatasetImporter.ImportAsync(connectionString, recipe);
        await using var context = new TenantDbContext(Options(connectionString), new(recipe.TenantId(0)));
        var migrator = context.GetService<IMigrator>();
        const string previous = "20260918112051_AtomicOrderTransitions";
        const string tested = "20260923165133_ReadMemberPermission";
        await migrator.MigrateAsync(previous);
        var before = await MembershipDigest();
        await using (var blocker = new NpgsqlConnection(connectionString))
        {
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("LOCK TABLE memberships IN ROW SHARE MODE", blocker, transaction);
            await command.ExecuteNonQueryAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateAsync(tested));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        Assert.Equal(previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
        var timer = Stopwatch.StartNew();
        await migrator.MigrateAsync(tested);
        timer.Stop();
        output.WriteLine($"ReadMemberPermission migration, Small 1,050 memberships / 10,000 orders: {timer.Elapsed.TotalMilliseconds:F3} ms. Local DDL timing, not HTTP capacity.");
        Assert.Equal(before, await MembershipDigest());
        Assert.Equal(10_000, await context.WorkOrders.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1_050, await context.Memberships.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await context.Memberships.IgnoreQueryFilters().CountAsync(member => ((int)member.Permissions & 128) != 0));
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("UPDATE memberships SET permissions=256"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET permissions=128 WHERE tenant_id={recipe.TenantId(0)} AND user_id={recipe.UserId(0, 0)}");
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous));
        Assert.Equal(PostgresErrorCodes.CheckViolation, rollback.SqlState);
        // O Down rejeitado mantém tanto a migration aplicada quanto o grant persistido e a nova constraint válida.
        Assert.EndsWith("_ReadMemberPermission", (await context.Database.GetAppliedMigrationsAsync()).Last(), StringComparison.Ordinal);
        Assert.Equal(128, await context.Memberships.IgnoreQueryFilters()
            .Where(member => member.TenantId == recipe.TenantId(0) && member.UserId == recipe.UserId(0, 0))
            .Select(member => (int)member.Permissions).SingleAsync());
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET permissions=255 WHERE tenant_id={recipe.TenantId(0)} AND user_id={recipe.UserId(0, 0)}");
        var after = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("UPDATE memberships SET permissions=256"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, after.SqlState);

        async Task<string> MembershipDigest() => await context.Database.SqlQueryRaw<string>("""
            SELECT md5(string_agg(tenant_id::text || user_id::text || permissions::text || is_active::text, ',' ORDER BY tenant_id,user_id)) AS "Value" FROM memberships
            """).SingleAsync();
    }

    [Theory]
    [InlineData("Host=example.com;Database=orbis_perf_safe")]
    [InlineData("Host=localhost;Database=orbis_perf_safe")]
    [InlineData("Host=127.0.0.1;Database=orbis_production")]
    [InlineData("Host=127.0.0.1;Database=orbis_perf_a;Host=10.0.0.1")]
    [InlineData("Host=127.0.0.1;Database=orbis_perf_a-b")]
    public void UnsafeDestinationsAreRejectedBeforeConnection(string connection) =>
        Assert.Throws<InvalidOperationException>(() => DatasetImporter.ValidateDestination(connection));

    [Fact]
    public async Task MembershipAccessMigrationPreservesSmallDataAndRefusesDestructiveDown()
    {
        var connection = await NewDatabaseAsync();
        var recipe = new DatasetRecipe(DatasetLevel.Small, DatasetProfile.Uniform, 42);
        var generated = await DatasetImporter.ImportAsync(connection, recipe);
        Assert.Equal("2", generated.GeneratorVersion);
        Assert.Equal(0, generated.Tables.Single(t => t.Table == "public.membership_access_changes").Rows);
        await using var context = new TenantDbContext(Options(connection), new(recipe.TenantId(0)));
        var migrator = context.GetService<IMigrator>();
        const string previous = "20260923165133_ReadMemberPermission";
        await migrator.MigrateAsync(previous);
        var before = await Digest();
        await using (var blocker = new NpgsqlConnection(connection))
        {
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("LOCK TABLE memberships IN ROW SHARE MODE", blocker, transaction);
            await command.ExecuteNonQueryAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateAsync());
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        Assert.Equal(previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
        var watch = Stopwatch.StartNew();
        await migrator.MigrateAsync();
        watch.Stop();
        output.WriteLine($"AtomicMembershipAccess migration, Small 1,050 memberships / 10,000 orders: {watch.Elapsed.TotalMilliseconds:F3} ms. Single local DDL sample, not capacity.");
        Assert.Equal(before, await Digest());
        Assert.Equal(1_050, await context.Memberships.IgnoreQueryFilters().CountAsync(m => m.Version == 1 && (m.Permissions & Orbis.Domain.Identity.Permission.ManageMembers) == 0));
        Assert.Equal(10_000, await context.WorkOrders.IgnoreQueryFilters().CountAsync());
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("UPDATE memberships SET permissions=512"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        invalid = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("UPDATE memberships SET version=0"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        foreach (var update in new[] { "UPDATE memberships SET permissions=256", "UPDATE memberships SET version=2" })
        {
            await context.Database.ExecuteSqlRawAsync(update);
            var rejected = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous));
            Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
            Assert.EndsWith("_AtomicMembershipAccess", (await context.Database.GetAppliedMigrationsAsync()).Last(), StringComparison.Ordinal);
            await context.Database.ExecuteSqlRawAsync("UPDATE memberships SET permissions=0,version=1");
        }
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO membership_access_changes (tenant_id,actor_id,key,member_id,fingerprint,previous_permissions,previous_is_active,previous_version,permissions,is_active,version,occurred_at)
            VALUES ({recipe.TenantId(0)},{recipe.UserId(0, 0)},{Guid.NewGuid()},{recipe.UserId(0, 1)},{new string('A', 64)},0,true,1,0,false,2,{DateTimeOffset.UnixEpoch})
            """);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous))).SqlState);
        Assert.Equal(1, await context.MembershipAccessChanges.IgnoreQueryFilters().CountAsync());

        Task<string> Digest() => context.Database.SqlQueryRaw<string>("""
            SELECT md5(string_agg(tenant_id::text || user_id::text || permissions::text || is_active::text, ',' ORDER BY tenant_id,user_id)) AS "Value" FROM memberships
            """).SingleAsync();
    }

    [Fact]
    public void RecipeIsStableAcrossCulturesAndHasRealisticIdentityAndTimeDistribution()
    {
        var recipe = new DatasetRecipe(DatasetLevel.Small, DatasetProfile.Uniform, 42);
        var original = CultureInfo.CurrentCulture;
        SyntheticOrder[] first, second;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            first = recipe.Orders().ToArray();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            second = recipe.Orders().ToArray();
        }
        finally { CultureInfo.CurrentCulture = original; }
        Assert.Equal(first, second);
        Assert.Equal(10_000, first.Select(o => o.Id).Distinct().Count());
        Assert.All(first, o => Assert.Equal(7, o.Id.Version));
        Assert.Equal([5_000, 1_000, 1_000, 1_000, 1_500, 500], first.GroupBy(o => o.Status).OrderBy(g => g.Key).Select(g => g.Count()));
        Assert.Equal(24, first.Select(o => (o.CreatedAt.Year, o.CreatedAt.Month)).Distinct().Count());
        Assert.NotEqual(first[0].Id, new DatasetRecipe(DatasetLevel.Small, DatasetProfile.Uniform, 43).Orders().First().Id);
        Assert.Equal(1_000_000, new DatasetRecipe(DatasetLevel.Large, DatasetProfile.HotTenant, 42).UserCount);
        Assert.Equal(10_000_000, new DatasetRecipe(DatasetLevel.Large, DatasetProfile.HotTenant, 42).OrderCount);
    }

    [Theory]
    [InlineData(DatasetProfile.Uniform)]
    [InlineData(DatasetProfile.HotTenant)]
    public async Task IndependentImportsProduceIdenticalContentAndValidHistory(DatasetProfile profile)
    {
        var recipe = new DatasetRecipe(DatasetLevel.Small, profile, 42);
        var firstConnection = await NewDatabaseAsync();
        var first = await DatasetImporter.ImportAsync(firstConnection, recipe);
        var second = await DatasetImporter.ImportAsync(await NewDatabaseAsync(), recipe);
        Assert.Equal(first.Tables.Select(t => (t.Table, t.Rows, t.ContentSha256)), second.Tables.Select(t => (t.Table, t.Rows, t.ContentSha256)));
        Assert.Equal(1_000, first.Tables.Single(t => t.Table == "directory.users").Rows);
        Assert.Equal(1_050, first.Tables.Single(t => t.Table == "public.memberships").Rows);
        Assert.Equal(10_000, first.Tables.Single(t => t.Table == "public.work_orders").Rows);
        Assert.Equal(recipe.Orders().Sum(o => o.Version), first.Tables.Single(t => t.Table == "public.work_order_audit").Rows);
        Assert.Equal(recipe.OrdersForTenant(0), first.Tenants.Single(t => t.TenantId == recipe.TenantId(0)).Orders);
        Assert.Equal(50, first.Tenants.Length);
        Assert.Equal(10_000, first.Tenants.Sum(t => t.Orders));
        Assert.All(first.Tables, t => { Assert.True(t.TotalBytes > 0); Assert.Equal(64, t.ContentSha256.Length); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatasetImporter.ImportAsync(firstConnection, recipe));
        await using var verify = new NpgsqlConnection(firstConnection);
        await verify.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM work_orders", verify);
        Assert.Equal(10_000L, await count.ExecuteScalarAsync());
        await VerifyReplayAndRlsAsync(firstConnection, recipe);
    }

    [Fact]
    public async Task UnknownTablesArePreservedAndPreventMigrations()
    {
        var connectionString = await NewDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand("CREATE TABLE valuable_data (id integer PRIMARY KEY); INSERT INTO valuable_data VALUES (123)", connection);
        await create.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatasetImporter.ImportAsync(connectionString, new(DatasetLevel.Small, DatasetProfile.Uniform, 42)));
        await using var check = new NpgsqlCommand("SELECT id, to_regclass('directory.tenants') IS NULL FROM valuable_data", connection);
        await using var reader = await check.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync()); Assert.Equal(123, reader.GetInt32(0)); Assert.True(reader.GetBoolean(1));
    }

    [Fact]
    public async Task ConstraintFailureRollsBackAllImportedBusinessRows()
    {
        var connectionString = await NewDatabaseAsync();
        var recipe = new DatasetRecipe(DatasetLevel.Small, DatasetProfile.Uniform, 42);
        await using var directory = DirectoryContext(connectionString);
        await directory.Database.MigrateAsync();
        await using var tenant = new TenantDbContext(Options(connectionString), new(recipe.TenantId(0)));
        await tenant.Database.MigrateAsync();
        await tenant.Database.ExecuteSqlRawAsync("ALTER TABLE order_transition_receipts ADD CONSTRAINT test_reject_import CHECK (version<5)");
        var error = await Assert.ThrowsAsync<PostgresException>(() => DatasetImporter.ImportAsync(connectionString, recipe));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(0, await tenant.WorkOrders.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await tenant.OrderAudit.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await tenant.CreationReceipts.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await directory.Users.CountAsync());
        Assert.Equal(0, await directory.Tenants.CountAsync());
    }

    private async Task VerifyReplayAndRlsAsync(string admin, DatasetRecipe recipe)
    {
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var grants = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "runtime-grants.sql")), connection);
        await grants.ExecuteNonQueryAsync();
        var runtime = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ORBIS_TEST_RUNTIME_CONNECTION")!) { Database = connection.Database }.ConnectionString;
        temporaryConnections.Add(runtime);
        var options = Options(runtime);
        await using var directory = DirectoryContext(runtime);
        var resolver = new ResolveTenantUser(new TenantDirectory(directory));
        var order = recipe.Orders().First(o => o.Status == WorkOrderStatus.Completed);
        var creation = await new CreateWorkOrder(resolver, new WorkOrderCreator(options, TimeProvider.System)).ExecuteAsync(
            new(recipe.Host(0), DatasetRecipe.Issuer, DatasetRecipe.Subject(order.CustomerId), null), recipe.Id("create-key", order.Index), order.Description, default);
        Assert.Equal(CreateOrderOutcome.Replayed, creation.Outcome);
        Assert.Equal(order.Id, creation.Order!.Id);
        var completion = await new TransitionWorkOrder(resolver, new WorkOrderTransitions(options, TimeProvider.System)).ExecuteAsync(
            new(recipe.Host(0), DatasetRecipe.Issuer, DatasetRecipe.Subject(order.ProviderId!.Value), null),
            new(order.Id, WorkOrderAction.Complete, 4, recipe.Id("transition-key", order.Index, 4), null), default);
        Assert.Equal(TransitionOrderOutcome.Replayed, completion.Outcome);
        Assert.Equal(5, completion.Order!.Version);
        await using var context = new TenantDbContext(options, new(recipe.TenantId(1)));
        await using var transaction = await context.BeginTenantTransactionAsync();
        Assert.Equal(recipe.OrdersForTenant(1), await context.WorkOrders.IgnoreQueryFilters().CountAsync());
        Assert.Null(await context.WorkOrders.IgnoreQueryFilters().SingleOrDefaultAsync(o => o.Id == order.Id));
        Assert.Null(await context.OrderAudit.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.OrderId == order.Id));
        await transaction.RollbackAsync();
        // Executa apenas duas amostras na suíte: verifica segurança/equivalência, não desempenho.
        var plans = await QueryPlanProbe.CaptureAsync(runtime, recipe, 2);
        Assert.Equal("orbis_runtime", plans.CurrentRole);
        Assert.True(plans.RowSecurityActive);
        Assert.Equal(recipe.OrdersForTenant(0) * 4 / 5, plans.DeepOffset);
        Assert.Equal(16, plans.Queries.Length);
        Assert.All(plans.Queries, q => { Assert.Equal(2, q.Samples.Length); Assert.True(q.P99Ms >= q.P50Ms); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => QueryPlanProbe.CaptureAsync(admin, recipe, 2));
    }

    private async Task<string> NewDatabaseAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ORBIS_TEST_ADMIN_CONNECTION")!);
        DatasetImporter.ValidateDestination(settings.ConnectionString);
        var name = "orbis_test_dataset_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        // Nome gerado internamente; nenhuma base existente é apagada, recriada ou truncada.
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        settings.Database = name;
        temporaryConnections.Add(settings.ConnectionString);
        return settings.ConnectionString;
    }

    private static DbContextOptions<TenantDbContext> Options(string connection) => new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connection).Options;
    private static DirectoryDbContext DirectoryContext(string connection) => new(new DbContextOptionsBuilder<DirectoryDbContext>()
        .UseNpgsql(connection, o => o.MigrationsHistoryTable("__DirectoryMigrationsHistory", "directory")).Options);
}
