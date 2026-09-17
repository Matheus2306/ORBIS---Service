using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class DirectoryMigrationTests(DatabaseFixture database)
{
    [Fact]
    public async Task ExistingTenantDataSurvivesMigrationButUnprovisionedIdentityRemainsSuspended()
    {
        await using var directory = database.CreateDirectoryContext();
        Assert.False((await directory.Tenants.SingleAsync(x => x.Id == database.LegacyTenant)).IsActive);
        Assert.False((await directory.Users.SingleAsync(x => x.Id == database.LegacyUser)).IsActive);
        Assert.False(await directory.Identities.AnyAsync(x => x.UserId == database.LegacyUser));
        await using var tenant = database.CreateContext(database.LegacyTenant);
        await using var transaction = await tenant.BeginTenantTransactionAsync();
        Assert.Equal(1, await tenant.WorkOrders.CountAsync());
        Assert.False(tenant.Database.HasPendingModelChanges());
        Assert.False(directory.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task RuntimeCannotProvisionOrActivateTenants()
    {
        await using var directory = database.CreateDirectoryContext();
        var tenant = await directory.Tenants.SingleAsync(x => x.Id == database.TenantA);
        tenant.Suspend();
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => directory.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }
}
