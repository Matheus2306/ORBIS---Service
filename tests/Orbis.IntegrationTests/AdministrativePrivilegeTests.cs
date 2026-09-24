using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Administration.Api;
using Orbis.Hosting;
using Orbis.Infrastructure.Persistence;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class AdministrativePrivilegeTests(DatabaseFixture database)
{
    [Theory]
    [InlineData("full-update")]
    [InlineData("tenant-key")]
    [InlineData("order-read")]
    [InlineData("history-delete")]
    [InlineData("history-update")]
    [InlineData("missing-version")]
    [InlineData("grant-option")]
    [InlineData("unforced-rls")]
    public async Task AdministrativeGuardRejectsCapabilityDriftAndRecovers(string scenario)
    {
        var (change, restore) = scenario switch
        {
            "full-update" => ("GRANT UPDATE ON memberships TO orbis_membership_admin", "REVOKE UPDATE ON memberships FROM orbis_membership_admin; GRANT UPDATE (permissions,is_active,version) ON memberships TO orbis_membership_admin"),
            "tenant-key" => ("GRANT UPDATE (tenant_id) ON memberships TO orbis_membership_admin", "REVOKE UPDATE (tenant_id) ON memberships FROM orbis_membership_admin"),
            "order-read" => ("GRANT SELECT (id) ON work_orders TO orbis_membership_admin", "REVOKE SELECT (id) ON work_orders FROM orbis_membership_admin"),
            "history-delete" => ("GRANT DELETE ON membership_access_changes TO orbis_membership_admin", "REVOKE DELETE ON membership_access_changes FROM orbis_membership_admin"),
            "history-update" => ("GRANT UPDATE (permissions) ON membership_access_changes TO orbis_membership_admin", "REVOKE UPDATE (permissions) ON membership_access_changes FROM orbis_membership_admin"),
            "missing-version" => ("REVOKE UPDATE (version) ON memberships FROM orbis_membership_admin", "GRANT UPDATE (version) ON memberships TO orbis_membership_admin"),
            "grant-option" => ("GRANT UPDATE (version) ON memberships TO orbis_membership_admin WITH GRANT OPTION", "REVOKE GRANT OPTION FOR UPDATE (version) ON memberships FROM orbis_membership_admin"),
            "unforced-rls" => ("ALTER TABLE memberships NO FORCE ROW LEVEL SECURITY", "ALTER TABLE memberships FORCE ROW LEVEL SECURITY"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        await using var owner = database.CreateContext(database.TenantA, admin: true);
        await using var source = NpgsqlDataSource.Create(database.MembershipConnection);
        using var guard = new AdministrativeDatabaseCheck(source, TimeProvider.System);
        await guard.StartAsync(CancellationToken.None);
        await owner.Database.ExecuteSqlRawAsync(change);
        try { await Assert.ThrowsAsync<InvalidOperationException>(() => guard.StartAsync(CancellationToken.None)); }
        finally { await owner.Database.ExecuteSqlRawAsync(restore); }
        await guard.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DatabaseCredentialsAreNotInterchangeable()
    {
        await using var service = NpgsqlDataSource.Create(database.RuntimeConnection);
        await using var administration = NpgsqlDataSource.Create(database.MembershipConnection);
        Assert.False(await DatabasePrivileges.IsSafeAsync(service, DatabaseAccessProfile.MembershipAdministration, CancellationToken.None));
        Assert.False(await DatabasePrivileges.IsSafeAsync(administration, DatabaseAccessProfile.Service, CancellationToken.None));
    }

    [Fact]
    public void AdministrativeConfigurationRequiresDistinctAudienceExplicitMfaAndAllowedClients()
    {
        var authentication = new AuthenticationSettings { Authority = ApiFactory.Issuer, Audience = AdministrationFactory.Audience };
        var valid = new AdministrationSettings { CommonApiAudience = ApiFactory.Audience, RequiredAcr = AdministrationFactory.Acr, AllowedClientIds = ["client"] };
        Assert.True(valid.IsValid(authentication));
        valid.CommonApiAudience = authentication.Audience;
        Assert.False(valid.IsValid(authentication));
        valid.CommonApiAudience = ApiFactory.Audience;
        valid.MaximumAuthenticationAgeSeconds = 901;
        Assert.False(valid.IsValid(authentication));
        valid.MaximumAuthenticationAgeSeconds = 900;
        valid.RequiredAcr = "";
        Assert.False(valid.IsValid(authentication));
        valid.RequiredAcr = AdministrationFactory.Acr;
        valid.AllowedClientIds = [];
        Assert.False(valid.IsValid(authentication));
    }
}
