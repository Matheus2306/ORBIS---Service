using System.Data.Common;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orbis.Application.Memberships;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Domain.Tenancy;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class MemberAccessChangeTests(DatabaseFixture database)
{
    private const Permission Admin = Permission.ManageMembers | Permission.ReadMembers | Permission.ReadOwnOrders;

    [Fact]
    public async Task ChangeReplayConflictSuspendAndReactivatePreserveAtomicHistory()
    {
        var scope = await SeedAsync();
        var command = new ChangeMemberAccessCommand(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid());
        var first = await Execute(scope, command);
        Assert.Equal(ChangeMemberAccessOutcome.Applied, first.Outcome);
        Assert.Equal(2, first.Member!.Version);
        var replay = await Execute(scope, command);
        Assert.Equal(ChangeMemberAccessOutcome.Replayed, replay.Outcome);
        Assert.Equal(first.Member, replay.Member);
        Assert.Equal(ChangeMemberAccessOutcome.Conflict, (await Execute(scope, command with { IsActive = false })).Outcome);
        Assert.Equal(ChangeMemberAccessOutcome.Conflict, (await Execute(scope, command with { Key = Guid.NewGuid(), Permissions = Permission.None })).Outcome);
        var suspend = command with { Key = Guid.NewGuid(), IsActive = false, ExpectedVersion = 2 };
        Assert.Equal(ChangeMemberAccessOutcome.Applied, (await Execute(scope, suspend)).Outcome);
        Assert.Equal(ChangeMemberAccessOutcome.Applied, (await Execute(scope, suspend with { Key = Guid.NewGuid(), IsActive = true, ExpectedVersion = 3 })).Outcome);
        await using var context = database.CreateContext(scope.Tenant, connection: database.MembershipConnection);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var member = await context.Memberships.SingleAsync(m => m.UserId == scope.Target);
        Assert.True(member.IsActive);
        Assert.Equal(4, member.Version);
        var history = await context.MembershipAccessChanges.OrderBy(c => c.Version).ToArrayAsync();
        Assert.Equal(3, history.Length);
        Assert.Equal(new long[] { 1, 2, 3 }, history.Select(c => c.PreviousVersion));
        Assert.Equal(new long[] { 2, 3, 4 }, history.Select(c => c.Version));
        Assert.Equal(Permission.ReadOwnOrders, history[0].PreviousPermissions);
        Assert.All(history, c => Assert.Equal(scope.Actor, c.ActorId));
    }

    [Theory]
    [InlineData("no-permission")]
    [InlineData("suspended-actor")]
    [InlineData("stronger-target")]
    [InlineData("excess-grant")]
    [InlineData("foreign-target")]
    [InlineData("foreign-tenant")]
    [InlineData("inactive-user")]
    [InlineData("inactive-tenant")]
    [InlineData("inactive-target-user")]
    public async Task PersistedAuthorizationDeniesAbuseWithoutHistory(string scenario)
    {
        var scope = await SeedAsync();
        var command = new ChangeMemberAccessCommand(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid());
        await using (var owner = database.CreateContext(scope.Tenant, admin: true))
        {
            if (scenario is "no-permission" or "suspended-actor" or "stronger-target")
            {
                await using var transaction = await owner.BeginTenantTransactionAsync();
                var member = await owner.Memberships.SingleAsync(m => m.UserId == (scenario == "stronger-target" ? scope.Target : scope.Actor));
                member.ChangeAccess(scenario == "stronger-target" ? Permission.ManageOrders : Permission.ReadMembers, scenario != "suspended-actor");
                await owner.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            if (scenario == "inactive-user") await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE directory.users SET is_active=false WHERE id={scope.Actor}");
            if (scenario == "inactive-tenant") await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE directory.tenants SET is_active=false WHERE id={scope.Tenant}");
            if (scenario == "inactive-target-user") await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE directory.users SET is_active=false WHERE id={scope.Target}");
        }
        if (scenario == "excess-grant") command = command with { Permissions = Permission.ManageOrders };
        if (scenario == "foreign-target") command = command with { MemberId = database.UserB };
        var actor = scenario == "foreign-tenant" ? new TenantUser(database.TenantB, scope.Actor) : new(scope.Tenant, scope.Actor);
        var result = await Service().ExecuteAsync(actor, command, CancellationToken.None);
        Assert.Equal(ChangeMemberAccessOutcome.Denied, result.Outcome);
        Assert.Null(result.Member);
        Assert.Equal(0, await HistoryCount(scope));
    }

    [Fact]
    public async Task ResolverRejectsForgedHostAndClaimAndReplaysReauthorizeActor()
    {
        var scope = await SeedAsync();
        var command = new ChangeMemberAccessCommand(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid());
        await using var directory = database.CreateDirectoryContext();
        var application = new ChangeMemberAccess(new(new TenantDirectory(directory)), Service());
        var request = new TenantRequest(scope.Host, ApiFactory.Issuer, scope.Actor.ToString(), null);
        Assert.Equal(ChangeMemberAccessOutcome.Denied, (await application.ExecuteAsync(request with { Host = database.HostB }, command, CancellationToken.None)).Outcome);
        Assert.Equal(ChangeMemberAccessOutcome.Denied, (await application.ExecuteAsync(request with { ClaimedTenantId = database.TenantB }, command, CancellationToken.None)).Outcome);
        Assert.Equal(ChangeMemberAccessOutcome.Applied, (await application.ExecuteAsync(request, command, CancellationToken.None)).Outcome);
        await using var owner = database.CreateContext(scope.Tenant, admin: true);
        await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET is_active=false,version=version+1 WHERE tenant_id={scope.Tenant} AND user_id={scope.Actor}");
        Assert.Equal(ChangeMemberAccessOutcome.Denied, (await application.ExecuteAsync(request, command, CancellationToken.None)).Outcome);
        Assert.Equal(1, await HistoryCount(scope));
    }

    [Fact]
    public async Task OneHundredSameKeyRetriesProduceExactlyOneChange()
    {
        var scope = await SeedAsync();
        var command = new ChangeMemberAccessCommand(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid());
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Execute(scope, command)));
        Assert.Single(results, r => r.Outcome == ChangeMemberAccessOutcome.Applied);
        Assert.All(results, r => Assert.Contains(r.Outcome, new[] { ChangeMemberAccessOutcome.Applied, ChangeMemberAccessOutcome.Replayed }));
        Assert.All(results, r => Assert.Equal(2, r.Member!.Version));
        Assert.Equal(1, await HistoryCount(scope));
    }

    [Fact]
    public async Task OneHundredCompetingKeysCannotOverwriteCommittedVersion()
    {
        var scope = await SeedAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Execute(scope,
            new(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid()))));
        Assert.Single(results, r => r.Outcome == ChangeMemberAccessOutcome.Applied);
        Assert.All(results, r => Assert.Contains(r.Outcome, new[] { ChangeMemberAccessOutcome.Applied, ChangeMemberAccessOutcome.Conflict }));
        Assert.Equal(1, await HistoryCount(scope));
    }

    [Fact]
    public async Task ReusedKeyWithConcurrentDifferentTargetsCommitsOnlyOneFingerprint()
    {
        var scope = await SeedAsync();
        var key = Guid.NewGuid();
        var service = Service(new TwoTransactionBarrier("membership_access_changes"));
        var results = await Task.WhenAll(new[] { scope.Target, scope.SecondAdmin }.Select(id => service.ExecuteAsync(new(scope.Tenant, scope.Actor),
            new(id, Permission.ReadMembers, true, 1, key), CancellationToken.None)));
        Assert.Single(results, r => r.Outcome == ChangeMemberAccessOutcome.Applied);
        Assert.Single(results, r => r.Outcome == ChangeMemberAccessOutcome.Conflict);
        Assert.Equal(1, await HistoryCount(scope));
    }

    [Fact]
    public async Task RevocationCommittedBeforeSnapshotDeniesWaitingCommand()
    {
        var scope = await SeedAsync(secondAdmin: true);
        var pause = new PauseBeforeSnapshot();
        var pending = Service(pause).ExecuteAsync(new(scope.Tenant, scope.Actor),
            new(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid()), CancellationToken.None);
        await pause.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(ChangeMemberAccessOutcome.Applied, (await Service().ExecuteAsync(new(scope.Tenant, scope.SecondAdmin),
                new(scope.Actor, Permission.ReadMembers, true, 1, Guid.NewGuid()), CancellationToken.None)).Outcome);
        }
        finally { pause.Continue.TrySetResult(); }
        Assert.Equal(ChangeMemberAccessOutcome.Denied, (await pending).Outcome);
        Assert.Equal(1, await HistoryCount(scope));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LastActiveAdministratorSurvivesConcurrentSelfRevocations(bool suspend)
    {
        var scope = await SeedAsync(secondAdmin: true);
        long retries = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) =>
        {
            if (instrument.Name == "orbis.membership.transaction_retries") meter.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref retries, value));
        listener.Start();
        var barrier = new TwoTransactionBarrier("m.permissions & 256");
        var service = Service(barrier);
        var results = await Task.WhenAll(new[] { scope.Actor, scope.SecondAdmin }.Select(id => service.ExecuteAsync(new(scope.Tenant, id),
            new(id, suspend ? Admin : Permission.ReadMembers, !suspend, 1, Guid.NewGuid()), CancellationToken.None)));
        Assert.Single(results, r => r.Outcome == ChangeMemberAccessOutcome.Applied);
        Assert.Single(results, r => r.Outcome == ChangeMemberAccessOutcome.Conflict);
        Assert.True(retries > 0, "Overlapping snapshots must exercise an actual serialization retry.");
        await using var context = database.CreateContext(scope.Tenant, connection: database.MembershipConnection);
        await using var transaction = await context.BeginTenantTransactionAsync();
        Assert.Equal(1, await context.Memberships.CountAsync(m => m.IsActive && (m.Permissions & Permission.ManageMembers) != 0));
        Assert.Equal(1, await context.MembershipAccessChanges.CountAsync());
    }

    [Fact]
    public async Task InactiveGlobalAdministratorDoesNotCountAsSurvivingAccess()
    {
        var scope = await SeedAsync(secondAdmin: true);
        await using var owner = database.CreateContext(scope.Tenant, admin: true);
        await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE directory.users SET is_active=false WHERE id={scope.SecondAdmin}");
        Assert.Equal(ChangeMemberAccessOutcome.Conflict,
            (await Execute(scope, new(scope.Actor, Permission.ReadMembers, true, 1, Guid.NewGuid()))).Outcome);
        Assert.Equal(0, await HistoryCount(scope));
    }

    [Fact]
    public async Task ExhaustedSerializationBudgetReturnsBusyAndCancellationIsNotRetried()
    {
        var scope = await SeedAsync();
        var fault = new SerializationFault();
        var command = new ChangeMemberAccessCommand(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid());
        Assert.Equal(ChangeMemberAccessOutcome.Busy,
            (await Service(fault).ExecuteAsync(new(scope.Tenant, scope.Actor), command, CancellationToken.None)).Outcome);
        Assert.Equal(3, fault.Attempts);
        Assert.Equal(0, await HistoryCount(scope));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().ExecuteAsync(new(scope.Tenant, scope.Actor), command, cancellation.Token));
        Assert.Equal(0, await HistoryCount(scope));
    }

    [Fact]
    public async Task FailureToPersistAuditRollsBackTheAccessChange()
    {
        var scope = await SeedAsync();
        await using var owner = database.CreateContext(scope.Tenant, admin: true);
        // Falha real do banco durante SaveChanges, sem mock do repositório; escopo restrito à identidade sintética.
        await owner.Database.OpenConnectionAsync();
        await using (var ddl = new NpgsqlCommand($"ALTER TABLE membership_access_changes ADD CONSTRAINT reject_test_actor CHECK (actor_id <> '{scope.Actor:D}'::uuid)",
            (NpgsqlConnection)owner.Database.GetDbConnection()))
            await ddl.ExecuteNonQueryAsync();
        try
        {
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => Execute(scope, new(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid())));
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        finally { await owner.Database.ExecuteSqlRawAsync("ALTER TABLE membership_access_changes DROP CONSTRAINT reject_test_actor"); }
        await using var transaction = await owner.BeginTenantTransactionAsync();
        var target = await owner.Memberships.SingleAsync(m => m.UserId == scope.Target);
        Assert.Equal(1, target.Version);
        Assert.Equal(Permission.ReadOwnOrders, target.Permissions);
        Assert.Equal(0, await owner.MembershipAccessChanges.CountAsync());
    }

    [Fact]
    public async Task AdministrativeRoleCannotEscapeRlsMutateAuditOrChangeIdentifiers()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        await Execute(a, new(a.Target, Permission.ReadMembers, true, 1, Guid.NewGuid()));
        await Execute(b, new(b.Target, Permission.ReadMembers, true, 1, Guid.NewGuid()));
        await using (var context = database.CreateContext(a.Tenant, connection: database.MembershipConnection))
        {
            Assert.Empty(await context.MembershipAccessChanges.IgnoreQueryFilters().ToArrayAsync());
            await using var transaction = await context.BeginTenantTransactionAsync();
            Assert.Equal(a.Tenant, Assert.Single(await context.MembershipAccessChanges.IgnoreQueryFilters().ToArrayAsync()).TenantId);
            Assert.Null(await context.Memberships.IgnoreQueryFilters().SingleOrDefaultAsync(m => m.UserId == b.Actor));
            Assert.Equal(0, await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET is_active=false WHERE user_id={b.Actor}"));
            var receipt = await context.MembershipAccessChanges.SingleAsync();
            context.MembershipAccessChanges.Remove(receipt);
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }
        foreach (var sql in new[] { "UPDATE memberships SET tenant_id=tenant_id", "INSERT INTO memberships (tenant_id,user_id,permissions,is_active) SELECT tenant_id,user_id,permissions,is_active FROM memberships",
            "UPDATE membership_access_changes SET version=version", "DELETE FROM membership_access_changes", "SELECT * FROM work_orders",
            "UPDATE directory.users SET is_active=true", "TRUNCATE memberships" })
        {
            await using var context = database.CreateContext(a.Tenant, connection: database.MembershipConnection);
            await using var transaction = await context.BeginTenantTransactionAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        await using (var context = database.CreateContext(a.Tenant, connection: database.MembershipConnection))
        {
            await using var transaction = await context.BeginTenantTransactionAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO membership_access_changes (tenant_id,actor_id,key,member_id,fingerprint,previous_permissions,previous_is_active,previous_version,permissions,is_active,version,occurred_at)
                VALUES ({b.Tenant},{b.Actor},{Guid.NewGuid()},{b.Target},{new string('A', 64)},1,true,1,0,true,2,{DateTimeOffset.UnixEpoch})
                """));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
    }

    [Fact]
    public async Task CommonRuntimeCannotExecuteCommandsReadHistoryOrResolveAdministrativeServices()
    {
        var scope = await SeedAsync();
        var service = new MemberAccessChanges(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(database.RuntimeConnection).Options, TimeProvider.System);
        var error = await Assert.ThrowsAsync<PostgresException>(() => service.ExecuteAsync(new(scope.Tenant, scope.Actor),
            new(scope.Target, Permission.ReadMembers, true, 1, Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        await using var application = new ApiFactory(database);
        Assert.Null(application.Services.GetService<IMemberAccessChanges>());
        Assert.Null(application.Services.GetService<ChangeMemberAccess>());
        Assert.Equal(0, await HistoryCount(scope));
    }

    private MemberAccessChanges Service(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<TenantDbContext>()
        .UseNpgsql(database.MembershipConnection).AddInterceptors(interceptors).Options, TimeProvider.System);

    private Task<ChangeMemberAccessResult> Execute(Scope scope, ChangeMemberAccessCommand command) =>
        Service().ExecuteAsync(new(scope.Tenant, scope.Actor), command, CancellationToken.None);

    private async Task<int> HistoryCount(Scope scope)
    {
        await using var context = database.CreateContext(scope.Tenant, connection: database.MembershipConnection);
        await using var transaction = await context.BeginTenantTransactionAsync();
        return await context.MembershipAccessChanges.CountAsync();
    }

    private async Task<Scope> SeedAsync(bool secondAdmin = false)
    {
        var scope = new Scope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var directory = database.CreateDirectoryContext(admin: true);
        var tenant = new Tenant(scope.Tenant, "Administrative test");
        tenant.Activate();
        var domain = new TenantDomain(scope.Tenant, scope.Host);
        domain.MarkVerified();
        directory.AddRange(tenant, domain);
        foreach (var id in new[] { scope.Actor, scope.Target, scope.SecondAdmin })
            directory.AddRange(new UserAccount(id), new ExternalIdentity(id, ApiFactory.Issuer, id.ToString()));
        await directory.SaveChangesAsync();
        await using var context = database.CreateContext(scope.Tenant, admin: true);
        await using var transaction = await context.BeginTenantTransactionAsync();
        context.AddRange(new Membership(scope.Tenant, scope.Actor, Admin), new Membership(scope.Tenant, scope.Target, Permission.ReadOwnOrders),
            new Membership(scope.Tenant, scope.SecondAdmin, secondAdmin ? Admin : Permission.None));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return scope;
    }

    private sealed record Scope(Guid Tenant, Guid Actor, Guid Target, Guid SecondAdmin)
    {
        public string Host => $"admin-{Tenant:N}.orbis.test";
    }

    private sealed class TwoTransactionBarrier(string marker) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrived;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains(marker, StringComparison.Ordinal)) return result;
            if (Interlocked.Increment(ref arrived) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }

    private sealed class PauseBeforeSnapshot : DbCommandInterceptor
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("set_config", StringComparison.Ordinal)) return result;
            // SERIALIZABLE fixa o snapshot no primeiro comando, inclusive SET via SELECT; a pausa precisa antecedê-lo.
            Arrived.TrySetResult();
            await Continue.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }

    private sealed class SerializationFault : DbCommandInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("directory.tenants", StringComparison.Ordinal)) return ValueTask.FromResult(result);
            // Injeção controlada só mede o limite de retries; conflitos reais são exercitados nos testes com barreira.
            Attempts++;
            throw new PostgresException("Injected serialization failure", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
        }
    }
}
