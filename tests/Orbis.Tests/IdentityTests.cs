using Orbis.Domain.Identity;
using Orbis.Domain.Tenancy;

namespace Orbis.Tests;

public sealed class IdentityTests
{
    [Theory]
    [InlineData("ACME.Orbis.Test.", "acme.orbis.test")]
    [InlineData("bücher.example", "xn--bcher-kva.example")]
    public void HostNormalizationUsesExactDnsName(string input, string expected) =>
        Assert.Equal(expected, TenantDomain.NormalizeHost(input));

    [Theory]
    [InlineData("acme.orbis.test:443")]
    [InlineData("https://acme.orbis.test")]
    [InlineData("acme.orbis.test/other")]
    [InlineData("*.orbis.test")]
    [InlineData("acme..orbis.test")]
    [InlineData("127.0.0.1")]
    [InlineData("acme.orbis.test ")]
    public void HostIsNotAnArbitraryUrlOrWildcard(string input) =>
        Assert.ThrowsAny<ArgumentException>(() => TenantDomain.NormalizeHost(input));

    [Fact]
    public void TenantAndDomainBeginUnavailableUntilProvisioned()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme");
        var domain = new TenantDomain(tenant.Id, "acme.orbis.test");
        Assert.False(tenant.IsActive);
        Assert.False(domain.IsVerified);
        tenant.Activate();
        domain.MarkVerified();
        Assert.True(tenant.IsActive);
        Assert.True(domain.IsVerified);
        tenant.Suspend();
        Assert.False(tenant.IsActive);
    }

    [Fact]
    public void SubjectIsOpaqueAndIssuerCannotBeAnInsecureUrl()
    {
        var user = Guid.NewGuid();
        var identity = new ExternalIdentity(user, "https://identity.orbis.test", "CaseSensitiveSubject");
        Assert.Equal("CaseSensitiveSubject", identity.Subject);
        Assert.Throws<ArgumentException>(() => new ExternalIdentity(user, "http://identity.orbis.test", "subject"));
    }
}
