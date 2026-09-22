using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Orbis.Application.WorkOrders;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class OrderListingTests(DatabaseFixture database)
{
    [Fact]
    public async Task KeysetTraversesEqualTimestampsWithoutDuplicatesAfterANewFirstPageInsert()
    {
        Guid[] expected;
        await using (var seed = database.CreateContext(database.TenantA))
        {
            await using var transaction = await seed.BeginTenantTransactionAsync();
            for (var i = 0; i < 7; i++) seed.Add(WorkOrder.Request(database.TenantA, database.UserA, $"Tie {i}", DateTimeOffset.UnixEpoch));
            await seed.SaveChangesAsync();
            await transaction.CommitAsync();
            await using var read = await seed.BeginTenantTransactionAsync();
            expected = await seed.WorkOrders.Where(order => order.CustomerUserId == database.UserA)
                .OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id).Select(order => order.Id).ToArrayAsync();
        }
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        var page = (await client.GetFromJsonAsync<OrderPage>("/v1/work-orders?limit=2"))!;
        Assert.Equal(2, page.Items.Count);
        Assert.NotNull(page.NextCursor);
        var visited = page.Items.Select(order => order.Id).ToList();
        await using (var insert = database.CreateContext(database.TenantA))
        {
            await using var transaction = await insert.BeginTenantTransactionAsync();
            insert.Add(WorkOrder.Request(database.TenantA, database.UserA, "Arrived after first page", DateTimeOffset.UtcNow.AddMinutes(1)));
            await insert.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        for (var pages = 0; page.NextCursor is not null; pages++)
        {
            Assert.True(pages < 100, "Pagination must terminate.");
            page = (await client.GetFromJsonAsync<OrderPage>($"/v1/work-orders?limit=2&cursor={page.NextCursor}"))!;
            Assert.InRange(page.Items.Count, 1, 2);
            visited.AddRange(page.Items.Select(order => order.Id));
        }
        Assert.Equal(expected, visited);
        Assert.Equal(visited.Count, visited.Distinct().Count());
    }

    [Fact]
    public async Task CursorIsBoundToTenantAndActorButNeverGrantsReadAuthority()
    {
        await using var application = new ApiFactory(database);
        using var allInA = Client(application, database.HostA, database.MultiTenantUser);
        var first = (await allInA.GetFromJsonAsync<OrderPage>("/v1/work-orders?limit=1"))!;
        Assert.NotNull(first.NextCursor);
        using var sameActorB = Client(application, database.HostB, database.MultiTenantUser);
        using var foreignTenant = await sameActorB.GetAsync($"/v1/work-orders?cursor={first.NextCursor}");
        Assert.Equal(HttpStatusCode.NotFound, foreignTenant.StatusCode);
        using var ownInA = Client(application, database.HostA, database.UserA);
        using var foreignActor = await ownInA.GetAsync($"/v1/work-orders?cursor={first.NextCursor}");
        Assert.Equal(HttpStatusCode.NotFound, foreignActor.StatusCode);

        // Mesmo editando o cursor para o ator atual, a permissão do autor original não é herdada.
        var forged = (OrderCursor.Decode(first.NextCursor!)! with { UserId = database.UserA }).Encode();
        var restricted = (await ownInA.GetFromJsonAsync<OrderPage>($"/v1/work-orders?limit=100&cursor={forged}"))!;
        await using var verify = database.CreateContext(database.TenantA);
        await using var transaction = await verify.BeginTenantTransactionAsync();
        var allowed = await verify.WorkOrders.Where(order => order.CustomerUserId == database.UserA).Select(order => order.Id).ToListAsync();
        Assert.All(restricted.Items, item => Assert.Contains(item.Id, allowed));
        Assert.DoesNotContain(restricted.Items, item => item.Id == database.OtherOrderA || item.Id == database.OrderB);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=-1")]
    [InlineData("limit=101")]
    [InlineData("limit=invalid")]
    [InlineData("cursor=invalid!")]
    [InlineData("cursor=bnVsbA")]
    [InlineData("cursor=")]
    [InlineData("cursor=%20")]
    [InlineData("limit=1&limit=100")]
    [InlineData("cursor=&cursor=")]
    public async Task InvalidPageRequestsAreRejected(string query)
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        using var response = await client.GetAsync($"/v1/work-orders?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProviderSeesOnlyAssignedOrdersAndPermissionChangesApplyToNextPage()
    {
        Guid assigned;
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using (var transaction = await admin.BeginTenantTransactionAsync())
        {
            var order = WorkOrder.Request(database.TenantA, database.UserA, "Provider assignment", DateTimeOffset.UtcNow);
            order.Assign(database.OtherUserA);
            admin.Add(order);
            await admin.SaveChangesAsync();
            await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET permissions={(int)Permission.ExecuteAssignedOrders} WHERE user_id={database.OtherUserA}");
            await transaction.CommitAsync();
            assigned = order.Id;
        }
        try
        {
            await using var application = new ApiFactory(database);
            using var client = Client(application, database.HostA, database.OtherUserA);
            var page = (await client.GetFromJsonAsync<OrderPage>("/v1/work-orders"))!;
            Assert.Equal(assigned, Assert.Single(page.Items).Id);
            Assert.Null(page.NextCursor);
            using var forbiddenOwn = await client.GetAsync($"/v1/work-orders/{database.OtherOrderA}");
            Assert.Equal(HttpStatusCode.NotFound, forbiddenOwn.StatusCode);
            await using (var revoke = await admin.BeginTenantTransactionAsync())
            {
                await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET permissions=0 WHERE user_id={database.OtherUserA}");
                await revoke.CommitAsync();
            }
            using var revoked = await client.GetAsync("/v1/work-orders");
            Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        }
        finally
        {
            await using var restore = await admin.BeginTenantTransactionAsync();
            await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET permissions={(int)(Permission.ReadOwnOrders | Permission.CreateOrders)} WHERE user_id={database.OtherUserA}");
            await restore.CommitAsync();
        }
    }

    private static HttpClient Client(ApiFactory application, string host, Guid user)
    {
        var client = application.CreateClient();
        client.BaseAddress = new Uri($"https://{host}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(user));
        return client;
    }
}
