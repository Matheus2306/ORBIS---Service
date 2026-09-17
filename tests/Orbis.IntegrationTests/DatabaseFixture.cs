using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Domain.Tenancy;
using Orbis.Domain.WorkOrders;
using Orbis.Infrastructure.Persistence;

namespace Orbis.IntegrationTests;

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public Guid TenantA { get; } = Guid.NewGuid();
    public Guid TenantB { get; } = Guid.NewGuid();
    public Guid UserA { get; } = Guid.NewGuid();
    public Guid UserB { get; } = Guid.NewGuid();
    public Guid OtherUserA { get; } = Guid.NewGuid();
    public Guid MultiTenantUser { get; } = Guid.NewGuid();
    public Guid LegacyTenant { get; } = Guid.NewGuid();
    public Guid LegacyUser { get; } = Guid.NewGuid();
    public string HostA => $"a-{TenantA:N}.orbis.test";
    public string HostB => $"b-{TenantB:N}.orbis.test";
    public string UnverifiedHost => $"unverified-{TenantA:N}.orbis.test";
    public Guid OrderA { get; private set; }
    public Guid OrderB { get; private set; }
    public Guid OtherOrderA { get; private set; }
    public Guid MultiTenantOrderB { get; private set; }
    public string RuntimeConnection { get; private set; } = string.Empty;
    private string AdminConnection { get; set; } = string.Empty;

    public async Task InitializeAsync()
    {
        AdminConnection = RequiredConnection("ORBIS_TEST_ADMIN_CONNECTION");
        RuntimeConnection = RequiredConnection("ORBIS_TEST_RUNTIME_CONNECTION");
        await using var admin = CreateContext(TenantA, admin: true);
        // Exercita a evolução de uma versão já populada, sem resetar nem apagar o legado.
        await admin.GetService<IMigrator>().MigrateAsync("20260917130050_InitialTenantBoundary");
        await SeedAsync(LegacyTenant, LegacyUser);
        await using var directory = CreateDirectoryContext(admin: true);
        await directory.Database.MigrateAsync();
        await admin.Database.MigrateAsync();
        // Esta credencial privilegiada existe apenas no fixture; a API não recebe acesso DDL.
        await admin.Database.ExecuteSqlRawAsync("""
            REVOKE ALL ON SCHEMA public FROM PUBLIC;
            GRANT USAGE ON SCHEMA public TO orbis_runtime;
            GRANT SELECT ON memberships TO orbis_runtime;
            GRANT SELECT, INSERT, UPDATE ON work_orders TO orbis_runtime;
            GRANT SELECT, INSERT ON work_order_audit, order_creation_receipts TO orbis_runtime;
            REVOKE ALL ON SCHEMA directory FROM PUBLIC;
            GRANT USAGE ON SCHEMA directory TO orbis_runtime;
            GRANT SELECT ON directory.tenants, directory.tenant_domains, directory.users, directory.external_identities TO orbis_runtime;
            """);
        foreach (var (id, host) in new[] { (TenantA, HostA), (TenantB, HostB) })
        {
            var tenant = new Tenant(id, "Synthetic tenant");
            tenant.Activate();
            var domain = new TenantDomain(id, host);
            domain.MarkVerified();
            directory.AddRange(tenant, domain);
        }
        directory.Domains.Add(new TenantDomain(TenantA, UnverifiedHost));
        foreach (var id in new[] { UserA, UserB, OtherUserA, MultiTenantUser })
            directory.AddRange(new UserAccount(id), new ExternalIdentity(id, ApiFactory.Issuer, id.ToString()));
        await directory.SaveChangesAsync();
        OrderA = await SeedAsync(TenantA, UserA);
        OrderB = await SeedAsync(TenantB, UserB);
        OtherOrderA = await SeedAsync(TenantA, OtherUserA);
        await SeedAsync(TenantA, MultiTenantUser, Permission.ReadAllOrders);
        MultiTenantOrderB = await SeedAsync(TenantB, MultiTenantUser);
    }

    public TenantDbContext CreateContext(Guid tenantId, bool admin = false, string? connection = null) =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connection ?? (admin ? AdminConnection : RuntimeConnection), options => options.CommandTimeout(10))
            .Options, new TenantScope(tenantId));

    public DirectoryDbContext CreateDirectoryContext(bool admin = false) => new(
        new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseNpgsql(admin ? AdminConnection : RuntimeConnection,
                options => options.MigrationsHistoryTable("__DirectoryMigrationsHistory", "directory"))
            .Options);

    private async Task<Guid> SeedAsync(Guid tenantId, Guid userId, Permission permissions = Permission.ReadOwnOrders | Permission.CreateOrders)
    {
        await using var context = CreateContext(tenantId, admin: true);
        await using var transaction = await context.BeginTenantTransactionAsync();
        context.Memberships.Add(new Membership(tenantId, userId, permissions));
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
