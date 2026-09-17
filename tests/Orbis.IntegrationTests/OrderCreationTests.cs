using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class OrderCreationTests(DatabaseFixture database)
{
    [Fact]
    public async Task ConcurrentHttpRetriesReturnOneStableReceiptAndOneAudit()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        var key = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("Idempotency-Key", key.ToString());
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync("/v1/work-orders", new { description = "  HTTP retry  " })));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var receipts = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<CreatedOrder>()));
        Assert.All(receipts, receipt => Assert.Equal(receipts[0], receipt));
        Assert.Single(responses, response => response.Headers.GetValues("Idempotency-Replayed").Single() == "false");
        Assert.All(responses, response => Assert.Equal($"/v1/work-orders/{receipts[0]!.Id}", response.Headers.Location!.OriginalString));
        foreach (var response in responses) response.Dispose();

        using var replay = await client.PostAsJsonAsync("/v1/work-orders", new { description = "HTTP retry" });
        Assert.Equal(receipts[0], await replay.Content.ReadFromJsonAsync<CreatedOrder>());
        using var conflict = await client.PostAsJsonAsync("/v1/work-orders", new { description = "Different effect" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertSingleEffect(key, receipts[0]!.Id);
    }

    [Fact]
    public async Task OneHundredConcurrentCommandsCommitExactlyOneEffect()
    {
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Create(database.TenantA, database.UserA, key, "100 contenders")));
        Assert.Single(results, result => result.Outcome == CreateOrderOutcome.Created);
        Assert.Equal(99, results.Count(result => result.Outcome == CreateOrderOutcome.Replayed));
        Assert.All(results, result => Assert.Equal(results[0].Order, result.Order));
        await AssertSingleEffect(key, results[0].Order!.Id);
    }

    [Fact]
    public async Task KeyNamespaceIsIndependentPerActorAndTenant()
    {
        var key = Guid.NewGuid();
        var a = await Create(database.TenantA, database.UserA, key, "A");
        var otherA = await Create(database.TenantA, database.OtherUserA, key, "other A");
        var b = await Create(database.TenantB, database.UserB, key, "B");
        Assert.All(new[] { a, otherA, b }, result => Assert.Equal(CreateOrderOutcome.Created, result.Outcome));
        Assert.Equal(3, new[] { a.Order!.Id, otherA.Order!.Id, b.Order!.Id }.Distinct().Count());

        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        Assert.Null(await context.CreationReceipts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrderId == b.Order.Id));
        Assert.Empty(await context.OrderAudit.IgnoreQueryFilters().Where(x => x.OrderId == b.Order.Id).ToListAsync());
    }

    [Fact]
    public async Task NoTenantContextCannotReadReceiptOrAudit()
    {
        await Create(database.TenantA, database.UserA, Guid.NewGuid(), "Private receipt");
        await using var context = database.CreateContext(database.TenantA);
        Assert.Empty(await context.CreationReceipts.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.OrderAudit.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task FailureToWriteAuditRollsBackOrderAndReceiptThenSameKeyCanSucceed()
    {
        var key = Guid.NewGuid();
        var description = $"Rollback {Guid.NewGuid()}";
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        // Falha real no último conjunto de escritas, sem mock de SaveChanges/transação.
        await admin.Database.ExecuteSqlRawAsync("ALTER TABLE work_order_audit ADD CONSTRAINT test_audit_failure CHECK (false) NOT VALID");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Create(database.TenantA, database.UserA, key, description));
            await using var verify = database.CreateContext(database.TenantA);
            await using var transaction = await verify.BeginTenantTransactionAsync();
            Assert.False(await verify.WorkOrders.AnyAsync(x => x.Description == description));
            Assert.False(await verify.CreationReceipts.AnyAsync(x => x.Key == key));
        }
        finally { await admin.Database.ExecuteSqlRawAsync("ALTER TABLE work_order_audit DROP CONSTRAINT test_audit_failure"); }
        var result = await Create(database.TenantA, database.UserA, key, description);
        Assert.Equal(CreateOrderOutcome.Created, result.Outcome);
        await AssertSingleEffect(key, result.Order!.Id);
    }

    [Theory]
    [InlineData("UPDATE work_order_audit SET action='work-order.requested'")]
    [InlineData("DELETE FROM work_order_audit")]
    [InlineData("TRUNCATE work_order_audit")]
    [InlineData("UPDATE order_creation_receipts SET fingerprint=repeat('A',64)")]
    [InlineData("DELETE FROM order_creation_receipts")]
    [InlineData("TRUNCATE order_creation_receipts")]
    public async Task RuntimeCannotRewriteOrEraseCommandHistory(string sql)
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-key")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidIdempotencyKeysAreRejected(string? key)
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        if (key is not null) client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        using var response = await client.PostAsJsonAsync("/v1/work-orders", new { description = "Invalid key" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgedAuthorityFieldsAreRejectedAndHostDoesNotGrantMembership()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var forged = await client.PostAsJsonAsync("/v1/work-orders", new { description = "Forged", tenantId = database.TenantB, customerId = database.UserB });
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        using var crossTenant = Client(application, database.HostB, database.UserA);
        crossTenant.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var denied = await crossTenant.PostAsJsonAsync("/v1/work-orders", new { description = "Forbidden" });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var readOnly = Client(application, database.HostA, database.MultiTenantUser);
        readOnly.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var noPermission = await readOnly.PostAsJsonAsync("/v1/work-orders", new { description = "Forbidden" });
        Assert.Equal(HttpStatusCode.NotFound, noPermission.StatusCode);
    }

    [Fact]
    public async Task RevokedMembershipCannotReplayPreviouslyCreatedReceipt()
    {
        var key = Guid.NewGuid();
        Assert.Equal(CreateOrderOutcome.Created, (await Create(database.TenantA, database.UserA, key, "Replay revocation")).Outcome);
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var transaction = await admin.BeginTenantTransactionAsync();
        await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET is_active=false WHERE user_id={database.UserA}");
        await transaction.CommitAsync();
        try { Assert.Equal(CreateOrderOutcome.Denied, (await Create(database.TenantA, database.UserA, key, "Replay revocation")).Outcome); }
        finally
        {
            await using var restore = await admin.BeginTenantTransactionAsync();
            await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET is_active=true WHERE user_id={database.UserA}");
            await restore.CommitAsync();
        }
    }

    private Task<CreateOrderResult> Create(Guid tenant, Guid actor, Guid key, string description)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(database.RuntimeConnection).Options;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("work-order.create.v1\n" + description)));
        return new WorkOrderCreator(options, TimeProvider.System).CreateAsync(new TenantUser(tenant, actor), key, description, fingerprint, CancellationToken.None);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("null\0byte")]
    public async Task InvalidDescriptionIsAClientError(string? description)
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync("/v1/work-orders", new { description });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RawSqlCannotInsertHistoryIntoAnotherTenant(bool audit)
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => audit
            ? context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO work_order_audit (tenant_id,id,actor_id,order_id,action,occurred_at)
                VALUES ({database.TenantB},{Guid.NewGuid()},{database.UserB},{database.OrderB},'work-order.requested',{DateTimeOffset.UnixEpoch})
                """)
            : context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO order_creation_receipts (tenant_id,actor_id,key,order_id,fingerprint,created_at)
                VALUES ({database.TenantB},{database.UserB},{Guid.NewGuid()},{database.OrderB},repeat('A',64),{DateTimeOffset.UnixEpoch})
                """));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private async Task AssertSingleEffect(Guid key, Guid orderId)
    {
        await using var context = database.CreateContext(database.TenantA);
        await using var transaction = await context.BeginTenantTransactionAsync();
        Assert.Single(await context.WorkOrders.Where(x => x.Id == orderId).ToListAsync());
        var receipt = Assert.Single(await context.CreationReceipts.Where(x => x.Key == key && x.ActorId == database.UserA).ToListAsync());
        Assert.Equal(orderId, receipt.OrderId);
        var audit = Assert.Single(await context.OrderAudit.Where(x => x.OrderId == orderId).ToListAsync());
        Assert.Equal(database.UserA, audit.ActorId);
        Assert.Equal("work-order.requested", audit.Action);
    }

    private HttpClient Client(ApiFactory application, string host, Guid user)
    {
        var client = application.CreateClient();
        client.BaseAddress = new Uri($"https://{host}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(user));
        return client;
    }
}
