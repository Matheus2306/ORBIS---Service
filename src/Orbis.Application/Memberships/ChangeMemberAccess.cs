using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;

namespace Orbis.Application.Memberships;

public sealed record ChangeMemberAccessCommand(Guid MemberId, Permission? Permissions, bool? IsActive, long ExpectedVersion, Guid Key)
{
    public bool IsValid => MemberId != Guid.Empty && Key != Guid.Empty && ExpectedVersion > 0 &&
        (Permissions.HasValue || IsActive.HasValue) && (!Permissions.HasValue || Membership.ArePermissionsValid(Permissions.Value));

    // Campos ausentes são preservados dentro da transação; comandos completos mantêm seus fingerprints históricos.
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
        "membership.access.v1", MemberId.ToString("D"), Permissions.HasValue ? ((int)Permissions.Value).ToString(CultureInfo.InvariantCulture) : "unchanged",
        IsActive.HasValue ? IsActive.Value ? "active" : "suspended" : "unchanged", ExpectedVersion.ToString(CultureInfo.InvariantCulture)))));
}

public enum ChangeMemberAccessOutcome { Applied, Replayed, Denied, Invalid, Conflict, Busy }
public sealed record ChangedMemberAccess(Guid UserId, Permission Permissions, bool IsActive, long Version);
public sealed record ChangeMemberAccessResult(ChangeMemberAccessOutcome Outcome, ChangedMemberAccess? Member = null);

public interface IMemberAccessChanges
{
    Task<ChangeMemberAccessResult> ExecuteAsync(TenantUser actor, ChangeMemberAccessCommand command, CancellationToken cancellationToken);
}

public sealed class ChangeMemberAccess(ResolveTenantUser resolver, IMemberAccessChanges changes)
{
    public async Task<ChangeMemberAccessResult> ExecuteAsync(TenantRequest request, ChangeMemberAccessCommand command, CancellationToken cancellationToken)
    {
        if (!command.IsValid) return new(ChangeMemberAccessOutcome.Invalid);
        var actor = await resolver.ExecuteAsync(request, cancellationToken);
        return actor is null ? new(ChangeMemberAccessOutcome.Denied) : await changes.ExecuteAsync(actor, command, cancellationToken);
    }
}
