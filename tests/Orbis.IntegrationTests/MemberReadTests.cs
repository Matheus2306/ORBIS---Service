using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Orbis.Application.Memberships;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Domain.Tenancy;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class MemberReadTests(DatabaseFixture database)
{
    [Fact]
    public async Task AdminCanPageOnlyOwnTenantMembersWithStableOrderAndMinimalDetails()
    {
        var scope = await SeedAsync();
        await using var application = new ApiFactory(database);
        using var client = Client(application, scope);
        var ids = new List<Guid>();
        string? cursor = null;
        do
        {
            using var response = await client.GetAsync("/v1/members?limit=2" + (cursor is null ? "" : "&cursor=" + cursor));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var page = (await response.Content.ReadFromJsonAsync<MemberPage>())!;
            Assert.InRange(page.Items.Count, 1, 2);
            ids.AddRange(page.Items.Select(member => member.UserId));
            cursor = page.NextCursor;
            Assert.True(ids.Count <= scope.Users.Length, "A cursor must advance, never repeat a page.");
        } while (cursor is not null);
        Assert.Equal(scope.Users.OrderBy(id => id.ToString(), StringComparer.Ordinal), ids);
        using var detail = await client.GetAsync($"/v1/members/{scope.Users[1]}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.True(detail.Headers.CacheControl?.NoStore);
        using var json = await detail.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(new[] { "isActive", "permissions", "userId" }, json!.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(scope.Users[1], json.RootElement.GetProperty("userId").GetGuid());
        var active = (await client.GetFromJsonAsync<MemberPage>("/v1/members?status=active"))!;
        Assert.Equal(4, active.Items.Count);
        Assert.All(active.Items, member => Assert.True(member.IsActive));
        var suspended = (await client.GetFromJsonAsync<MemberPage>("/v1/members?status=suspended"))!;
        Assert.Equal(scope.Users[^1], Assert.Single(suspended.Items).UserId);
        Assert.False(suspended.Items[0].IsActive);
        Assert.Null(suspended.NextCursor);
        // Acesso à lista de membros não concede leitura de ordens nem escrita administrativa no banco.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/work-orders")).StatusCode);
        await using var runtime = database.CreateContext(scope.TenantId);
        await using var transaction = await runtime.BeginTenantTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => runtime.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE memberships SET permissions=255 WHERE user_id={scope.ActorId}"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task OrderPrivilegesDoNotPermitEnumeratingEvenOwnMembership()
    {
        var scope = await SeedAsync(Permission.ReadAllOrders | Permission.ManageOrders);
        await using var application = new ApiFactory(database);
        using var client = Client(application, scope);
        foreach (var path in new[] { "/v1/members", $"/v1/members/{scope.ActorId}", $"/v1/members/{database.UserB}" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/v1/me")).StatusCode);
    }

    [Fact]
    public async Task HostClaimsHeadersResourceIdsAndCursorsCannotCrossTenantOrActor()
    {
        var scope = await SeedAsync();
        var other = await SeedAsync();
        await using var application = new ApiFactory(database);
        using var client = Client(application, scope);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", other.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", other.Host);
        var page = (await client.GetFromJsonAsync<MemberPage>($"/v1/members?limit=1&tenantId={other.TenantId}"))!;
        Assert.Contains(page.Items[0].UserId, scope.Users);
        foreach (var id in new[] { other.ActorId, database.UserB, Guid.NewGuid(), Guid.Empty })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/members/{id}")).StatusCode);
        using var foreign = Client(application, other);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync("/v1/members?cursor=" + page.NextCursor)).StatusCode);
        await using (var context = database.CreateContext(other.TenantId, admin: true))
        {
            await using var transaction = await context.BeginTenantTransactionAsync();
            context.Memberships.Add(new Membership(other.TenantId, scope.ActorId, Permission.ReadAllOrders));
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        // A mesma identidade global tem direitos distintos em cada vínculo.
        using var multiTenant = Client(application, other with { ActorId = scope.ActorId });
        Assert.Equal(HttpStatusCode.NotFound, (await multiTenant.GetAsync("/v1/members")).StatusCode);
        using var colleague = Client(application, scope with { ActorId = scope.Users[1] });
        Assert.Equal(HttpStatusCode.NotFound, (await colleague.GetAsync("/v1/members?cursor=" + page.NextCursor)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/v1/members?status=active&cursor=" + page.NextCursor)).StatusCode);
        foreach (var host in new[] { other.Host, database.UnverifiedHost, scope.Host + ".attacker.example" })
        {
            using var wrongHost = Client(application, scope with { Host = host });
            Assert.Equal(HttpStatusCode.NotFound, (await wrongHost.GetAsync("/v1/members")).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", application.Token(scope.ActorId, tenantId: other.TenantId));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/members")).StatusCode);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("membership")]
    [InlineData("user")]
    [InlineData("tenant")]
    public async Task RevocationAppliesToListAndDetailWithExistingTokenAndCursor(string boundary)
    {
        var scope = await SeedAsync();
        await using var application = new ApiFactory(database);
        using var client = Client(application, scope);
        var page = (await client.GetFromJsonAsync<MemberPage>("/v1/members?limit=1"))!;
        await using var admin = database.CreateContext(scope.TenantId, admin: true);
        await using var directory = database.CreateDirectoryContext(admin: true);
        if (boundary is "permission" or "membership")
        {
            await using var transaction = await admin.BeginTenantTransactionAsync();
            if (boundary == "permission")
                await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET permissions=0 WHERE tenant_id={scope.TenantId} AND user_id={scope.ActorId}");
            else
            {
                (await admin.Memberships.SingleAsync(member => member.UserId == scope.ActorId)).Suspend();
                await admin.SaveChangesAsync();
            }
            await transaction.CommitAsync();
        }
        else
        {
            if (boundary == "user") (await directory.Users.SingleAsync(user => user.Id == scope.ActorId)).Suspend();
            else (await directory.Tenants.SingleAsync(tenant => tenant.Id == scope.TenantId)).Suspend();
            await directory.SaveChangesAsync();
        }
        foreach (var path in new[] { "/v1/members", "/v1/members?cursor=" + page.NextCursor, $"/v1/members/{scope.Users[1]}" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("limit=")]
    [InlineData("limit=private-marker")]
    [InlineData("limit=1&limit=2")]
    [InlineData("cursor=")]
    [InlineData("cursor=private-marker")]
    [InlineData("cursor=a&cursor=b")]
    [InlineData("status=")]
    [InlineData("status=private-marker")]
    [InlineData("status=active&status=suspended")]
    public async Task InvalidPageParametersReturnGenericProblemDetails(string query)
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, new(database.TenantA, database.HostA, database.UserA, []));
        using var response = await client.GetAsync("/v1/members?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("private-marker", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReaderUsesBoundedSeekAndRevalidatesPermissionWithoutGlobalDirectoryOrCount()
    {
        var scope = await SeedAsync();
        var capture = new QueryCapture();
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(database.RuntimeConnection).AddInterceptors(capture).Options;
        var reader = new MemberReader(options);
        var actor = new TenantUser(scope.TenantId, scope.ActorId);
        var first = (await reader.ListAsync(actor, 2, null, MemberStatusFilter.All, default))!;
        capture.Commands.Clear();
        var second = (await reader.ListAsync(actor, 2, first.Items[^1].UserId, MemberStatusFilter.All, default))!;
        Assert.Equal(2, capture.Commands.Count);
        var pageSql = capture.Commands[1];
        Assert.Contains(">", pageSql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", pageSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CASE", pageSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OFFSET", pageSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT", pageSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("directory", pageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(first.Items.Select(member => member.UserId).Intersect(second.Items.Select(member => member.UserId)));
        Assert.Null(await reader.FindAsync(actor, database.UserB, default));
        Assert.Null(await reader.ListAsync(new(scope.TenantId, database.UserB), 2, null, MemberStatusFilter.All, default));
        // Uma posição forjada pode pular registros, mas nunca muda tenant, filtro ou autorização efetiva.
        var empty = await reader.ListAsync(actor, 2, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), MemberStatusFilter.All, default);
        Assert.Empty(empty!.Items);
        Assert.False(empty.HasMore);
    }

    private sealed record Scope(Guid TenantId, string Host, Guid ActorId, Guid[] Users);

    private async Task<Scope> SeedAsync(Permission permissions = Permission.ReadMembers)
    {
        var tenantId = Guid.NewGuid();
        var users = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var host = $"members-{tenantId:N}.orbis.test";
        await using var directory = database.CreateDirectoryContext(admin: true);
        var tenant = new Tenant(tenantId, "Member test tenant");
        tenant.Activate();
        var domain = new TenantDomain(tenantId, host);
        domain.MarkVerified();
        directory.AddRange(tenant, domain);
        foreach (var id in users) directory.AddRange(new UserAccount(id), new ExternalIdentity(id, ApiFactory.Issuer, id.ToString()));
        await directory.SaveChangesAsync();
        await using var context = database.CreateContext(tenantId, admin: true);
        await using var transaction = await context.BeginTenantTransactionAsync();
        foreach (var id in users)
        {
            var member = new Membership(tenantId, id, permissions);
            if (id == users[^1]) member.Suspend();
            context.Memberships.Add(member);
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(tenantId, host, users[0], users);
    }

    private static HttpClient Client(ApiFactory application, Scope scope)
    {
        var client = application.CreateClient();
        client.BaseAddress = new Uri($"https://{scope.Host}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(scope.ActorId));
        return client;
    }

    private sealed class QueryCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            // Captura só o formato SQL; valores de parâmetros/identidades não entram em logs.
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
