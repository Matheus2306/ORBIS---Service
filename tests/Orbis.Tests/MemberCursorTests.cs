using Orbis.Application.Identity;
using Orbis.Application.Memberships;
using Orbis.Domain.Identity;

namespace Orbis.Tests;

public sealed class MemberCursorTests
{
    [Fact]
    public void PositionMustBeBoundedVersionedAndBoundToIdentityTenantAndFilter()
    {
        var cursor = new MemberCursor(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MemberStatusFilter.Active);
        Assert.Equal(cursor, MemberCursor.Decode(cursor.Encode()));
        foreach (var invalid in new[] { "", "%%", new string('a', 513), "bnVsbA",
            (cursor with { Version = 2 }).Encode(), (cursor with { TenantId = Guid.Empty }).Encode(),
            (cursor with { ActorId = Guid.Empty }).Encode(), (cursor with { AfterUserId = Guid.Empty }).Encode(),
            (cursor with { Status = (MemberStatusFilter)99 }).Encode() })
            Assert.Null(MemberCursor.Decode(invalid));
    }

    [Fact]
    public void MemberReadPermissionDoesNotGrantOrderAccessOrRecognizeUnknownFlags()
    {
        var member = new Membership(Guid.NewGuid(), Guid.NewGuid(), Permission.ReadMembers);
        Assert.True(member.Allows(Permission.ReadMembers));
        Assert.False(member.Allows(Permission.ReadAllOrders));
        Assert.False(member.Allows(Permission.ManageOrders));
        Assert.Equal(new[] { "ReadMembers" }, PermissionNames.From(member.Permissions));
        Assert.Empty(PermissionNames.From(Permission.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Membership(member.TenantId, member.UserId, (Permission)512));
        member.Suspend();
        Assert.False(member.Allows(Permission.ReadMembers));
    }
}
