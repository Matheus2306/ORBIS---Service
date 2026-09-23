using System.Globalization;
using Orbis.Application.Memberships;
using Orbis.Domain.Identity;

namespace Orbis.Tests;

public sealed class MembershipAccessTests
{
    [Fact]
    public void DelegationCannotCrossTenantsExceedActorsRightsOrControlStrongerMembers()
    {
        var tenant = Guid.NewGuid();
        var actor = new Membership(tenant, Guid.NewGuid(), Permission.ManageMembers | Permission.ReadMembers);
        var target = new Membership(tenant, Guid.NewGuid(), Permission.ReadMembers);
        Assert.True(MembershipAccess.CanChange(actor, target, Permission.None));
        Assert.True(MembershipAccess.CanChange(actor, target, actor.Permissions));
        Assert.False(MembershipAccess.CanChange(actor, target, Permission.ManageOrders));
        Assert.False(MembershipAccess.CanChange(actor, new(Guid.NewGuid(), target.UserId, Permission.None), Permission.None));
        Assert.False(MembershipAccess.CanChange(actor, new(tenant, target.UserId, Permission.ManageOrders), Permission.None));
        Assert.False(MembershipAccess.CanChange(actor, target, (Permission)512));
        actor.Suspend();
        Assert.False(MembershipAccess.CanChange(actor, target, Permission.None));
    }

    [Fact]
    public void EffectiveChangesAdvanceVersionAndUnchangedAccessDoesNot()
    {
        var member = new Membership(Guid.NewGuid(), Guid.NewGuid(), Permission.None);
        Assert.Equal(1, member.Version);
        member.Suspend();
        member.Suspend();
        Assert.Equal(2, member.Version);
        Assert.False(member.IsActive);
        Assert.True(member.ChangeAccess(Permission.ReadMembers, true));
        Assert.Equal(3, member.Version);
        Assert.True(member.Allows(Permission.ReadMembers));
        Assert.False(member.ChangeAccess(Permission.ReadMembers, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => member.ChangeAccess((Permission)(-1), true));
        Assert.Equal(3, member.Version);
    }

    [Fact]
    public void SelfRevocationRecordsBothSidesBeforeMutatingActor()
    {
        var member = new Membership(Guid.NewGuid(), Guid.NewGuid(), Permission.ManageMembers);
        var command = new ChangeMemberAccessCommand(member.UserId, Permission.None, true, 1, Guid.NewGuid());
        var change = MembershipAccessChange.Apply(member, member, command.Permissions, true, command.Key, command.Fingerprint(), DateTimeOffset.UnixEpoch);
        Assert.Equal(Permission.ManageMembers, change.PreviousPermissions);
        Assert.Equal(Permission.None, change.Permissions);
        Assert.Equal(1, change.PreviousVersion);
        Assert.Equal(2, change.Version);
        Assert.Equal(member.UserId, change.ActorId);
        Assert.False(member.Allows(Permission.ManageMembers));
    }

    [Fact]
    public void FingerprintIncludesEveryMutableInputAndIsCultureIndependent()
    {
        var command = new ChangeMemberAccessCommand(Guid.NewGuid(), Permission.ReadMembers, true, 1234, Guid.NewGuid());
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var first = command.Fingerprint();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal(first, command.Fingerprint());
            Assert.Equal(first, (command with { Key = Guid.NewGuid() }).Fingerprint());
            foreach (var changed in new[] { command with { MemberId = Guid.NewGuid() }, command with { Permissions = Permission.None },
                command with { IsActive = false }, command with { ExpectedVersion = 2 } })
                Assert.NotEqual(first, changed.Fingerprint());
        }
        finally { CultureInfo.CurrentCulture = original; }
        foreach (var invalid in new[] { command with { MemberId = Guid.Empty }, command with { Key = Guid.Empty },
            command with { ExpectedVersion = 0 }, command with { Permissions = (Permission)512 } })
            Assert.False(invalid.IsValid);
    }
}
