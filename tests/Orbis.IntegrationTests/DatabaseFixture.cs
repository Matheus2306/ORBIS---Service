using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;
using Orbis.Infrastructure.Persistence;

namespace Orbis.IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public Guid TenantA { get; } = Guid.NewGuid();
    public Guid TenantB { get; } = Guid.NewGuid();
    public Guid UserA { get; } = Guid.NewGuid();
    public Guid UserB { get; } = Guid.NewGuid();
    public Guid OrderA { get; private set; }
    public Guid OrderB { get; private set; }
    public string RuntimeConnection { get; private set; } = string.Empty;
    private string AdminConnection { get; set; } = string.Empty;

    public async Task InitializeAsync()
    {
        AdminConnection = RequiredConnection("ORBIS_TEST_ADMIN_CONNECTION");
        RuntimeConnection = RequiredConnection("ORBIS_TEST_RUNTIME_CONNECTION");
        await using var admin = CreateContext(TenantA, admin: true);
        await admin.Database.MigrateAsync();
        // Esta credencial privilegiada existe apenas no fixture; a API não recebe acesso DDL.
        await admin.Database.ExecuteSqlRawAsync("""
            REVOKE ALL ON SCHEMA public FROM PUBLIC;
            GRANT USAGE ON SCHEMA public TO orbis_runtime;
            GRANT SELECT ON memberships TO orbis_runtime;
            GRANT SELECT, INSERT, UPDATE ON work_orders TO orbis_runtime;
            """);
        OrderA = await SeedAsync(TenantA, UserA);
        OrderB = await SeedAsync(TenantB, UserB);
    }

    public TenantDbContext CreateContext(Guid tenantId, bool admin = false, string? connection = null) =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connection ?? (admin ? AdminConnection : RuntimeConnection), options => options.CommandTimeout(10))
            .Options, new TenantScope(tenantId));

    private async Task<Guid> SeedAsync(Guid tenantId, Guid userId)
    {
        await using var context = CreateContext(tenantId, admin: true);
        await using var transaction = await context.BeginTenantTransactionAsync();
        context.Memberships.Add(new Membership(tenantId, userId, Permission.ReadOwnOrders | Permission.CreateOrders));
        var order = WorkOrder.Request(tenantId, userId, "Synthetic private order", DateTimeOffset.UnixEpoch);
        context.WorkOrders.Add(order);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return order.Id;
    }

    private static string RequiredConnection(string name)
    {
        var value = Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"{name} is required. Run scripts/test-postgres.ps1; database tests must not be silently skipped.");
        var connection = new NpgsqlConnectionStringBuilder(value);
        if (connection.Host is not ("127.0.0.1" or "localhost") || !connection.Database!.StartsWith("orbis_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Integration tests require an explicitly isolated local database.");
        return value;
    }

    public Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        return Task.CompletedTask;
    }
}
