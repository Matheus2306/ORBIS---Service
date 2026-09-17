using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Orbis.Api;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class RuntimeDatabaseCheckTests(DatabaseFixture database)
{
    [Theory]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("TRUNCATE")]
    public async Task MembershipMutationPrivilegePreventsStartup(string privilege)
    {
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var source = NpgsqlDataSource.Create(database.RuntimeConnection);
        using var check = new RuntimeDatabaseCheck(source, TimeProvider.System);
        // Privilégios são sintaxe SQL, não valores parametrizáveis; a allowlist mantém o teste fechado.
        var (grant, revoke) = privilege switch
        {
            "INSERT" => ("GRANT INSERT ON memberships TO orbis_runtime", "REVOKE INSERT ON memberships FROM orbis_runtime"),
            "UPDATE" => ("GRANT UPDATE ON memberships TO orbis_runtime", "REVOKE UPDATE ON memberships FROM orbis_runtime"),
            "DELETE" => ("GRANT DELETE ON memberships TO orbis_runtime", "REVOKE DELETE ON memberships FROM orbis_runtime"),
            "TRUNCATE" => ("GRANT TRUNCATE ON memberships TO orbis_runtime", "REVOKE TRUNCATE ON memberships FROM orbis_runtime"),
            _ => throw new ArgumentException("Unsupported test privilege.", nameof(privilege))
        };
        await admin.Database.ExecuteSqlRawAsync(grant);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));
        }
        finally
        {
            await admin.Database.ExecuteSqlRawAsync(revoke);
        }
    }

    [Fact]
    public async Task ReadinessDetectsPrivilegeDriftAndRecoveryAfterBoundedStaleness()
    {
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var source = NpgsqlDataSource.Create(database.RuntimeConnection);
        var clock = new ProbeClock();
        using var check = new RuntimeDatabaseCheck(source, clock);
        await check.StartAsync(CancellationToken.None);
        await admin.Database.ExecuteSqlRawAsync("GRANT UPDATE ON memberships TO orbis_runtime");
        try
        {
            Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new())).Status);
            clock.Advance();
            var probes = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => check.CheckHealthAsync(new())));
            Assert.All(probes, result => Assert.Equal(HealthStatus.Unhealthy, result.Status));
        }
        finally
        {
            await admin.Database.ExecuteSqlRawAsync("REVOKE UPDATE ON memberships FROM orbis_runtime");
        }
        clock.Advance();
        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new())).Status);
    }

    private sealed class ProbeClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance() => timestamp += TimeSpan.FromSeconds(6).Ticks;
    }
}
