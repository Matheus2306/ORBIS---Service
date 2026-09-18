using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.DataGenerator;
using Orbis.Domain.WorkOrders;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class DatasetTests
{
    [Theory]
    [InlineData("Host=example.com;Database=orbis_perf_safe")]
    [InlineData("Host=localhost;Database=orbis_perf_safe")]
    [InlineData("Host=127.0.0.1;Database=orbis_production")]
    [InlineData("Host=127.0.0.1;Database=orbis_perf_a;Host=10.0.0.1")]
    [InlineData("Host=127.0.0.1;Database=orbis_perf_a-b")]
    public void UnsafeDestinationsAreRejectedBeforeConnection(string connection) =>
        Assert.Throws<InvalidOperationException>(() => DatasetImporter.ValidateDestination(connection));

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

    private static async Task VerifyReplayAndRlsAsync(string admin, DatasetRecipe recipe)
    {
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var grants = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "runtime-grants.sql")), connection);
        await grants.ExecuteNonQueryAsync();
        var runtime = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ORBIS_TEST_RUNTIME_CONNECTION")!) { Database = connection.Database }.ConnectionString;
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
    }

    private static async Task<string> NewDatabaseAsync()
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
        return settings.ConnectionString;
    }

    private static DbContextOptions<TenantDbContext> Options(string connection) => new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connection).Options;
    private static DirectoryDbContext DirectoryContext(string connection) => new(new DbContextOptionsBuilder<DirectoryDbContext>()
        .UseNpgsql(connection, o => o.MigrationsHistoryTable("__DirectoryMigrationsHistory", "directory")).Options);
}
