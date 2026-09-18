using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class OrderTransitionTests(DatabaseFixture database)
{
    [Fact]
    public async Task ExistingCreationAuditSurvivesMigrationWithVersionOne()
    {
        await using var context = database.CreateContext(database.LegacyTenant);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var audit = await context.OrderAudit.SingleAsync(entry => entry.Id == database.LegacyAuditId);
        Assert.Equal("work-order.requested", audit.Action);
        Assert.Equal(1, audit.OrderVersion);
        Assert.Equal(database.LegacyUser, audit.ActorId);
    }

    [Fact]
    public async Task HttpLifecycleEnforcesActorsAndPreservesOneAuditPerVersion()
    {
        var actors = await CreateActors();
        await using var application = new ApiFactory(database);
        using var customer = Client(application, actors.Customer);
        using var dispatcher = Client(application, actors.Dispatcher);
        using var provider = Client(application, actors.Provider);
        using var otherProvider = Client(application, actors.OtherProvider);
        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/work-orders")
        { Content = JsonContent.Create(new { description = "Complete HTTP journey" }) };
        createMessage.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var created = await customer.SendAsync(createMessage);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = (await created.Content.ReadFromJsonAsync<CreatedOrder>())!;
        var key = Guid.NewGuid();
        using var forbiddenAssign = await Post(customer, order.Id, "assign", 1, key, actors.Provider);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenAssign.StatusCode);
        using var assign = await Post(dispatcher, order.Id, "assign", 1, key, actors.Provider);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        using var forbiddenAccept = await Post(otherProvider, order.Id, "accept", 2, key);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenAccept.StatusCode);
        using var accept = await Post(provider, order.Id, "accept", 2, key);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        using var start = await Post(provider, order.Id, "start", 3, key);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var forbiddenCancel = await Post(customer, order.Id, "cancel", 4, key);
        Assert.Equal(HttpStatusCode.Conflict, forbiddenCancel.StatusCode);
        using var complete = await Post(provider, order.Id, "complete", 4, key);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var result = await complete.Content.ReadFromJsonAsync<TransitionedOrder>();
        Assert.Equal(new TransitionedOrder(order.Id, "Completed", 5), result);
        using var replay = await Post(provider, order.Id, "complete", 4, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(result, await replay.Content.ReadFromJsonAsync<TransitionedOrder>());
        using var secondCompletion = await Post(provider, order.Id, "complete", 5, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, secondCompletion.StatusCode);
        var current = await customer.GetFromJsonAsync<OrderDetails>($"/v1/work-orders/{order.Id}");
        Assert.Equal("Completed", current!.Status);

        await using var verify = database.CreateContext(database.TenantA);
        await using var transaction = await verify.BeginTenantTransactionAsync();
        var audit = await verify.OrderAudit.Where(entry => entry.OrderId == order.Id).OrderBy(entry => entry.OrderVersion).ToListAsync();
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, audit.Select(entry => entry.OrderVersion));
        Assert.Equal(new[] { actors.Customer, actors.Dispatcher, actors.Provider, actors.Provider, actors.Provider }, audit.Select(entry => entry.ActorId));
        Assert.Equal(4, await verify.TransitionReceipts.CountAsync(receipt => receipt.OrderId == order.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OneHundredConcurrentCompletionsCommitOneVersion(bool sameKey)
    {
        var actors = await CreateActors();
        var orderId = await SeedOrder(actors, WorkOrderStatus.InProgress);
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(database.RuntimeConnection).Options;
        var service = new WorkOrderTransitions(options, TimeProvider.System);
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => service.ExecuteAsync(
            new TenantUser(database.TenantA, actors.Provider),
            new(orderId, WorkOrderAction.Complete, 4, sameKey ? key : Guid.NewGuid(), null), new string('A', 64), CancellationToken.None)));
        Assert.Single(results, result => result.Outcome == TransitionOrderOutcome.Applied);
        Assert.Equal(99, results.Count(result => result.Outcome == (sameKey ? TransitionOrderOutcome.Replayed : TransitionOrderOutcome.Conflict)));
        await using var verify = database.CreateContext(database.TenantA);
        await using var transaction = await verify.BeginTenantTransactionAsync();
        var final = await verify.WorkOrders.SingleAsync(order => order.Id == orderId);
        Assert.Equal(WorkOrderStatus.Completed, final.Status);
        Assert.Equal(5, final.Version);
        Assert.Single(await verify.OrderAudit.Where(entry => entry.OrderId == orderId).ToListAsync());
        Assert.Single(await verify.TransitionReceipts.Where(receipt => receipt.OrderId == orderId).ToListAsync());
    }

    [Fact]
    public async Task StaleVersionDifferentPayloadAndInvalidStateCannotChangeOrder()
    {
        var actors = await CreateActors();
        var id = await SeedOrder(actors);
        await using var application = new ApiFactory(database);
        using var dispatcher = Client(application, actors.Dispatcher);
        using var provider = Client(application, actors.Provider);
        var key = Guid.NewGuid();
        using var assigned = await Post(dispatcher, id, "assign", 1, key, actors.Provider);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using var changedPayload = await Post(dispatcher, id, "assign", 1, key, actors.OtherProvider);
        Assert.Equal(HttpStatusCode.Conflict, changedPayload.StatusCode);
        using var stale = await Post(provider, id, "accept", 1, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var skippedState = await Post(provider, id, "start", 2, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, skippedState.StatusCode);
        using var providerOnCancel = await Post(dispatcher, id, "cancel", 2, Guid.NewGuid(), actors.Provider);
        Assert.Equal(HttpStatusCode.BadRequest, providerOnCancel.StatusCode);
        var result = await dispatcher.GetFromJsonAsync<OrderDetails>($"/v1/work-orders/{id}");
        Assert.Equal(2, result!.Version);
        Assert.Equal("Assigned", result.Status);
    }

    [Fact]
    public async Task FailedAuditRollsBackTransitionAndHttpErrorDoesNotExposeDatabaseDetails()
    {
        var actors = await CreateActors();
        var id = await SeedOrder(actors, WorkOrderStatus.InProgress);
        var key = Guid.NewGuid();
        await using var application = new ApiFactory(database);
        using var provider = Client(application, actors.Provider);
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await admin.Database.ExecuteSqlRawAsync("ALTER TABLE work_order_audit ADD CONSTRAINT test_transition_audit_failure CHECK (action <> 'work-order.completed') NOT VALID");
        try
        {
            using var failed = await Post(provider, id, "complete", 4, key);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.DoesNotContain("test_transition_audit_failure", await failed.Content.ReadAsStringAsync());
            await using var verify = database.CreateContext(database.TenantA);
            await using var transaction = await verify.BeginTenantTransactionAsync();
            var order = await verify.WorkOrders.SingleAsync(order => order.Id == id);
            Assert.Equal(WorkOrderStatus.InProgress, order.Status);
            Assert.Equal(4, order.Version);
            Assert.False(await verify.TransitionReceipts.AnyAsync(receipt => receipt.OrderId == id));
            Assert.False(await verify.OrderAudit.AnyAsync(entry => entry.OrderId == id));
        }
        finally { await admin.Database.ExecuteSqlRawAsync("ALTER TABLE work_order_audit DROP CONSTRAINT test_transition_audit_failure"); }
        using var recovered = await Post(provider, id, "complete", 4, key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task ForeignTenantInactiveProviderAndRevokedReplayAreDenied()
    {
        var actors = await CreateActors();
        var id = await SeedOrder(actors);
        await using var application = new ApiFactory(database);
        using var dispatcher = Client(application, actors.Dispatcher);
        using var foreign = Client(application, actors.Dispatcher, database.HostB);
        using var wrongHost = await Post(foreign, id, "assign", 1, Guid.NewGuid(), actors.Provider);
        Assert.Equal(HttpStatusCode.NotFound, wrongHost.StatusCode);
        using var foreignId = await Post(dispatcher, database.OrderB, "assign", 1, Guid.NewGuid(), actors.Provider);
        Assert.Equal(HttpStatusCode.NotFound, foreignId.StatusCode);
        using var foreignProvider = await Post(dispatcher, id, "assign", 1, Guid.NewGuid(), database.UserB);
        Assert.Equal(HttpStatusCode.NotFound, foreignProvider.StatusCode);
        await using var directory = database.CreateDirectoryContext(admin: true);
        (await directory.Users.SingleAsync(user => user.Id == actors.OtherProvider)).Suspend();
        await directory.SaveChangesAsync();
        using var inactiveProvider = await Post(dispatcher, id, "assign", 1, Guid.NewGuid(), actors.OtherProvider);
        Assert.Equal(HttpStatusCode.NotFound, inactiveProvider.StatusCode);
        var key = Guid.NewGuid();
        using var assigned = await Post(dispatcher, id, "assign", 1, key, actors.Provider);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        await using (var admin = database.CreateContext(database.TenantA, admin: true))
        {
            await using var transaction = await admin.BeginTenantTransactionAsync();
            (await admin.Memberships.SingleAsync(member => member.UserId == actors.Dispatcher)).Suspend();
            await admin.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        using var replay = await Post(dispatcher, id, "assign", 1, key, actors.Provider);
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
    }

    [Fact]
    public async Task CustomerCancellationIsScopedToOwnOrderAndRecordedOnce()
    {
        var actors = await CreateActors();
        var id = await SeedOrder(actors);
        await using var application = new ApiFactory(database);
        using var customer = Client(application, actors.Customer);
        using var wrongOwner = await Post(customer, database.OrderA, "cancel", 1, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, wrongOwner.StatusCode);
        var key = Guid.NewGuid();
        using var cancel = await Post(customer, id, "cancel", 1, key);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(new TransitionedOrder(id, "Cancelled", 2), await cancel.Content.ReadFromJsonAsync<TransitionedOrder>());
        using var replay = await Post(customer, id, "cancel", 1, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await using var verify = database.CreateContext(database.TenantA);
        await using var transaction = await verify.BeginTenantTransactionAsync();
        Assert.Single(await verify.OrderAudit.Where(entry => entry.OrderId == id).ToListAsync());
    }

    [Theory]
    [InlineData("UPDATE order_transition_receipts SET version=999")]
    [InlineData("DELETE FROM order_transition_receipts")]
    [InlineData("TRUNCATE order_transition_receipts")]
    public async Task RuntimeCannotRewriteTransitionReceipts(string sql)
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task TransitionReceiptRequiresTenantScopeForReadsAndWrites()
    {
        var actors = await CreateActors();
        var id = await SeedOrder(actors);
        await using var application = new ApiFactory(database);
        using var customer = Client(application, actors.Customer);
        using var cancel = await Post(customer, id, "cancel", 1, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        await using var absent = database.CreateContext(database.TenantA);
        Assert.Empty(await absent.TransitionReceipts.IgnoreQueryFilters().ToListAsync());
        await using var foreign = database.CreateContext(database.TenantB);
        await using var transaction = await foreign.BeginTenantTransactionAsync();
        Assert.Empty(await foreign.TransitionReceipts.IgnoreQueryFilters().Where(receipt => receipt.OrderId == id).ToListAsync());
        var error = await Assert.ThrowsAsync<PostgresException>(() => foreign.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO order_transition_receipts (tenant_id,actor_id,action,key,fingerprint,order_id,status,version,created_at)
            VALUES ({database.TenantA},{actors.Customer},5,{Guid.NewGuid()},repeat('A',64),{id},5,2,{DateTimeOffset.UnixEpoch})
            """));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private async Task<Actors> CreateActors()
    {
        var actors = new Actors(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var directory = database.CreateDirectoryContext(admin: true);
        foreach (var id in new[] { actors.Customer, actors.Dispatcher, actors.Provider, actors.OtherProvider })
            directory.AddRange(new UserAccount(id), new ExternalIdentity(id, ApiFactory.Issuer, id.ToString()));
        await directory.SaveChangesAsync();
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var transaction = await admin.BeginTenantTransactionAsync();
        admin.AddRange(new Membership(database.TenantA, actors.Customer, Permission.CreateOrders | Permission.ReadOwnOrders | Permission.CancelOwnOrders),
            new Membership(database.TenantA, actors.Dispatcher, Permission.ReadAllOrders | Permission.AssignOrders | Permission.ManageOrders),
            new Membership(database.TenantA, actors.Provider, Permission.ExecuteAssignedOrders),
            new Membership(database.TenantA, actors.OtherProvider, Permission.ExecuteAssignedOrders));
        await admin.SaveChangesAsync();
        await transaction.CommitAsync();
        return actors;
    }

    private async Task<Guid> SeedOrder(Actors actors, WorkOrderStatus status = WorkOrderStatus.Requested)
    {
        var order = WorkOrder.Request(database.TenantA, actors.Customer, "Synthetic transition order", DateTimeOffset.UnixEpoch);
        if (status == WorkOrderStatus.InProgress) { order.Assign(actors.Provider); order.Accept(actors.Provider); order.Start(actors.Provider); }
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        context.Add(order);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return order.Id;
    }

    private HttpClient Client(ApiFactory application, Guid user, string? host = null)
    {
        var client = application.CreateClient();
        client.BaseAddress = new Uri($"https://{host ?? database.HostA}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(user));
        return client;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, Guid order, string action, long version, Guid key, Guid? provider = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/work-orders/{order}/{action}")
        { Content = JsonContent.Create(new { expectedVersion = version, providerUserId = provider }) };
        request.Headers.Add("Idempotency-Key", key.ToString());
        return await client.SendAsync(request);
    }

    private sealed record Actors(Guid Customer, Guid Dispatcher, Guid Provider, Guid OtherProvider);
}
